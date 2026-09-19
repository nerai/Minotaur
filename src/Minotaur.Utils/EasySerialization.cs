using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using K4os.Compression.LZ4.Streams;

namespace Minotaur.Utils;

public class DataMissingException : Exception
{
	public DataMissingException (string? message)
		: base (message)
	{
	}
}

public class EasySerialization<T>
{
	private readonly string _CacheDir;

	// Note: This class is thread safe UNLESS called twice with the same name
	private readonly DataContractSerializer _Ser;

	public bool PrintTimes = false;
	public string FileExtension = "lz4";
	public bool Compress = true;

	public EasySerialization (string cacheDir)
	{
		_CacheDir = cacheDir ?? throw new ArgumentNullException (nameof (cacheDir));
		Directory.CreateDirectory (_CacheDir);

		var settings = new DataContractSerializerSettings () {
			SerializeReadOnlyTypes = true,
			PreserveObjectReferences = true,
		};
		_Ser = new DataContractSerializer (typeof (T), settings);
	}

	public void SetSurrogateProvider (ISerializationSurrogateProvider provider)
	{
		_Ser.SetSerializationSurrogateProvider (provider);
	}

	private static FileStream OpenPatiently (string path, FileMode mode, FileAccess access, FileShare share)
	{
		int attempt = 0;
		while (true) {
			try {
				var f = new FileStream (path, mode, access, share);
				return f;
			}
			catch (IOException ex) {
				attempt += 1;
				if (attempt < 10) {
					Logging.LogN ($"Error opening file, will try again:\n{ex}");
					Thread.Sleep (attempt * 100);
				}
				else {
					throw;
				}
			}
		}
	}

	public T LoadOrCreate (
		string name,
		Func<T> create,
		bool doNotLoadIfExisting = false)
	{
		var cachePathFinal = $"{_CacheDir}/{name}.{FileExtension}";

		if (File.Exists (cachePathFinal)) {
			if (doNotLoadIfExisting) {
				return default;
			}
			using var f = OpenPatiently (cachePathFinal, FileMode.Open, FileAccess.Read, FileShare.Read);
			object o;
			if (Compress) {
				using var z = LZ4Stream.Decode (f);
				o = _Ser.ReadObject (z);
			}
			else {
				o = _Ser.ReadObject (f);
			}
			var tree = (T) o;
			return tree;
		}
		else {
			if (create == null) {
				throw new DataMissingException ($"Failed to load data file {cachePathFinal}");
			}
			var o = create ();
			Store (name, o);
			return o;
		}
	}

	public bool TryLoad (string name, out T it)
	{
		var cachePathFinal = $"{_CacheDir}/{name}.{FileExtension}";

		if (!File.Exists (cachePathFinal)) {
			it = default;
			return false;
		}

		using var f = OpenPatiently (cachePathFinal, FileMode.Open, FileAccess.Read, FileShare.Read);
		object o;
		if (Compress) {
			using var z = LZ4Stream.Decode (f);
			o = _Ser.ReadObject (z);
		}
		else {
			o = _Ser.ReadObject (f);
		}
		it = (T) o;
		return true;
	}

	public void Store (string name, T data, bool overwrite = false)
	{
		var cachePathFinal = $"{_CacheDir}/{name}.{FileExtension}";
		var cachePathTmp = $"{cachePathFinal}.tmp";

		var dir = Path.GetDirectoryName (cachePathTmp);
		Directory.CreateDirectory (dir);

		Stopwatch sw = null;
		if (PrintTimes) {
			lock (Console.Out) {
				Console.WriteLine ($"Persisting {name}...");
			}
			sw = Stopwatch.StartNew ();
		}

		using (var f = OpenPatiently (cachePathTmp, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None)) {
			if (Compress) {
				using var z = LZ4Stream.Encode (f);
				_Ser.WriteObject (z, data);
			}
			else {
				_Ser.WriteObject (f, data);
			}
		}
		File.Move (cachePathTmp, cachePathFinal, overwrite);

		if (PrintTimes) {
			sw.Stop ();
			lock (Console.Out) {
				Console.WriteLine ($"Persisted {name} in {sw.Elapsed.TotalSeconds:0.0}s");
			}
		}
	}

	public void Delete (string name)
	{
		var cachePathFinal = $"{_CacheDir}/{name}.{FileExtension}";
		File.Delete (cachePathFinal);
	}

	public void ClearEverything ()
	{
		var files = Directory.EnumerateFiles (
			_CacheDir,
			$"*.{FileExtension}",
			SearchOption.TopDirectoryOnly);
		foreach (var file in files) {
			File.Delete (file);
		}
	}

	public ParallelQuery<(string name, T data)> LoadAll ()
	{
		var files = Directory.GetFiles (_CacheDir, $"*.{FileExtension}", SearchOption.AllDirectories);
		lock (Console.Out) {
			Console.WriteLine ($"Loading {files.Length} files in {_CacheDir}");
		}

		return files
			.AsParallel ()
			.Select (file => {
				var name = Path.GetRelativePath (_CacheDir, file);
				name = name.Substring (0, name.Length - FileExtension.Length - 1);
				var add = LoadOrCreate (name, null);
				return (name, add);
			});
	}

	public void Recreate ()
	{
		Console.WriteLine ($"RECREATE {_CacheDir}?");
		if (Console.ReadLine () != "y") {
			throw new Exception ();
		}

		LoadAll ().ForAll (pair => {
			Store (pair.name, pair.data, true);
		});
	}
}
