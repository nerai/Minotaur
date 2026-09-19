using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Minotaur.Utils;

/// <summary>
/// Logging utilities.
/// 
/// Note: These are NOT async - they will write immediately, and they will block.
/// </summary>
public static class Logging
{
	[ThreadStatic]
	private static SLogDisposable? __Ongoing;

	public static readonly object Background = new ();
	public static readonly object ResetColor = new ();

	private static void ApplyCurrentLogBlock (Action<SLogDisposable> a)
	{
		var slog = __Ongoing;

		if (slog != null) {
			/*
			 * Use the existing block if possible.
			 */
			lock (slog) {
				a (slog);
			}
		}
		else {
			/*
			 * Create a temporary new block.
			 * A queued block is needed because another thread may be occupying the output currently.
			 * It is not required to lock on the temp block because everything occurs within the current thread.
			 */
			using var tempSlog = (SLogDisposable) StartLogBlock;
			a (tempSlog);
		}
	}

	/// <summary>
	/// Log part of a line (or multiple lines).
	/// </summary>
	public static void Log (params object [] texts)
	{
		ApplyCurrentLogBlock (slog => slog.Log (texts, false));
	}

	/// <summary>
	/// Log a text fragment. Append this to the current line.
	/// </summary>
	public static void LogF (params object [] texts)
	{
		ApplyCurrentLogBlock (slog => slog.Log (texts, true));
	}

	/// <summary>
	/// Log a complete line (or multiple lines) and end it with a line break.
	/// </summary>
	public static void LogN (params object [] texts)
	{
		var par = texts.ToList ();
		par.Insert (0, "\n");
		Log (par.ToArray ());
	}

	public static IDisposable StartLogBlock {
		get {
			var slog = new SLogDisposable ();
			var old = Interlocked.CompareExchange (ref __Ongoing, slog, null);
			if (old == null) {
				// Was newly inserted, all good.
			}
			else {
				// Someone else was there first!
				// We will NOT update the block but will hope that the other block encompasses this one.
			}
			return slog;
		}
	}

	/// <summary>
	/// A self-contained block of log lines.
	/// This block is printed in a single step in its entirety, it is NOT mixed with any other logging.
	/// All, none or some of its lines may be prefixed.
	/// </summary>
	private class SLogDisposable : IDisposable
	{
		public readonly int ThreadID;
		public bool TeeToFile; // TODO

		private List<SingleLine> _Lines = new ();

		private class SingleLine
		{
			public List<(string what, Action a)> Actions = new ();
			public bool NeedsIndentation = false;
			public bool NeedsPrefix = false;
			public bool IsCompleted = false;
			public readonly DateTime tCreated;

			public SingleLine (DateTime t)
			{
				tCreated = t;
			}

			public void Apply ()
			{
				foreach (var pair in Actions) {
					pair.a ();
				}
			}

			internal void Add (string what, Action a)
			{
				Actions.Add ((what, a));
			}

			public void AddAction_FC (ConsoleColor? cc)
			{
				if (cc.HasValue) {
					Add ("Set foreground color", () => {
						Console.ForegroundColor = cc.Value;
					});
				}
			}

			public void AddAction_BC (ConsoleColor? cc)
			{
				if (cc.HasValue) {
					Add ("Set background color", () => {
						Console.BackgroundColor = cc.Value;
					});
				}
			}

			public void AddAction_RC ()
			{
				Add ("Reset color", () => {
					Console.ResetColor ();
				});
			}

			internal void AddAction_NL ()
			{
				Add ($"Write newline", () => {
					Console.Write ("\n");
					if (NeedsIndentation) {
						const int PrefixLength = 18;
						Console.BackgroundColor = ConsoleColor.DarkBlue;
						Console.ForegroundColor = ConsoleColor.White;
						if (NeedsPrefix) {
							var ctid = Environment.CurrentManagedThreadId;
							if (ctid == 1) {
								Console.BackgroundColor = ConsoleColor.DarkGreen;
							}
							Console.Write ($"[{ctid,3}]");
							if (ctid == 1) {
								Console.BackgroundColor = ConsoleColor.DarkBlue;
							}
							Console.Write ($" {tCreated:HH:mm:ss.fff}");
						}
						else {
							Console.Write (new string (' ', PrefixLength));
						}
						Console.ResetColor ();
						Console.Write (" ");
					}
				});
			}

			internal void AddAction_W (string s)
			{
				Add ($"Write <{s}>", () => {
					Console.Write (s);
				});
			}
		}

		private DateTime _Tcreated = DateTime.UtcNow;
		private bool _NextReadColorIsBackground = false;

		private ConsoleColor? CurrentFC = null;
		private ConsoleColor? CurrentBC = null;

		private SingleLine CurLine {
			get {
				if (_Lines.Count == 0 || _Lines.Last ().IsCompleted) {
					var add = new SingleLine (_Tcreated);
					add.AddAction_FC (CurrentFC);
					add.AddAction_BC (CurrentBC);
					if (_Lines.Count > 0 && _Lines.Last ().NeedsIndentation) {
						add.NeedsIndentation = true;
					}
					_Lines.Add (add);
				}
				return _Lines.Last ();
			}
		}

		public SLogDisposable ()
		{
			ThreadID = Environment.CurrentManagedThreadId;
		}

		public void Log (object [] rawInputs, bool isFragment)
		{
			/*
			 * If anything in this line is not a fragment, the entire line is not a fragment.
			 */
			CurLine.NeedsIndentation |= !isFragment;

			/*
			 * The first line that is not a fragment requires a prefix
			 */
			if (CurLine.NeedsIndentation && _Lines.All (line => !line.NeedsPrefix)) {
				CurLine.NeedsPrefix = true;
			}

			/*
			 * Resolve all parameters immediately - they might change if this is delayed
			 */
			foreach (var item in rawInputs) {
				if (item == Background) {
					_NextReadColorIsBackground = true;
					continue;
				}
				if (item == ResetColor) {
					_NextReadColorIsBackground = false;
					CurrentFC = null;
					CurrentBC = null;
					CurLine.AddAction_RC ();
					continue;
				}
				if (item is ConsoleColor cc) {
					if (_NextReadColorIsBackground) {
						_NextReadColorIsBackground = false;
						CurrentBC = cc;
						CurLine.AddAction_BC (cc);
					}
					else {
						CurrentFC = cc;
						CurLine.AddAction_FC (cc);
					}
					continue;
				}

				var split = item
					.ToString ()
					.Replace ("\r", "")
					.Split ('\n');
				for (int i = 0; i < split.Length; i++) {
					if (i != 0) {
						CurLine.AddAction_NL ();
						CurLine.IsCompleted = true;
					}
					var s = split [i];
					if (s.Length > 0) {
						CurLine.AddAction_W (s);
					}
				}
			}
			rawInputs = null;
		}

		public void Dispose ()
		{
			__Ongoing = null;

			if (_Lines.Count > 0) {
				lock (Console.Out) {
					foreach (var line in _Lines) {
						line.Apply ();
					}
					Console.ResetColor ();
				}
			}
		}
	}
}
