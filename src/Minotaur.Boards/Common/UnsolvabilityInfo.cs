using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Minotaur.Boards.Common;

public class UnsolvabilityInfo
{
	public enum UnsolvabilityReason
	{
		PieceDuplication,
		SSD,
		ISSD,
		SSS,
		CannotCompleteString,
		CannotKeepDancing, // only for dancing matrix solver
	}

	public readonly UnsolvabilityReason Reason;

	public readonly string OffendingObject;

	public readonly string MainReason;
	public readonly string Detail;

	public UnsolvabilityInfo (
		UnsolvabilityReason reason,
		string offendingObject,
		string mainReason,
		string detail)
	{
		Reason = reason;
		OffendingObject = offendingObject ?? throw new ArgumentNullException (nameof (offendingObject));
		MainReason = mainReason ?? throw new ArgumentNullException (nameof (mainReason));
		Detail = detail ?? throw new ArgumentNullException (nameof (detail));
	}

	public override string ToString ()
	{
		//return FullInfo;
		throw new NotImplementedException (); // TODO
	}

	public static string Aggregate (IList<UnsolvabilityInfo> infos)
	{
		if (infos.Count == 0) {
			throw new ArgumentException ();
		}

		var s = "";
		var groups = infos.GroupBy (info => info.Reason);

		if (groups.Count () == 1) {
			// empty
			s += "The following problem cannot be avoided:\n";
		}
		else {
			s += "Either way, one of the following problems is inevitable:\n";
		}

		foreach (var group in groups) {
			var offs = group.Select (info => info.OffendingObject).Distinct ().ToList ();
			var offs0 = offs [0];
			var offss = string.Join (", ", offs);

			switch (group.Key) {
				case UnsolvabilityReason.PieceDuplication: {
					if (offs.Count == 1) {
						s += $"Piece {offs0} will be duplicated.\n";
					}
					else {
						s += $"One of the pieces {offss} will be duplicated.\n";
					}
				}
				break;

				case UnsolvabilityReason.SSD: {
					if (offs.Count == 1) {
						s += $"Subspace cell count is not divisible by 5 in {offs0}\n";
					}
					else {
						s += $"Subspace cell count is not divisible by 5 in one of {offss}\n";
					}
				}
				break;

				case UnsolvabilityReason.ISSD: {
					if (offs.Count == 1) {
						s += $"Incomplete subspace cannot be completed in {offs0}\n";
					}
					else {
						s += $"Incomplete subspace cannot be completed in one of {offss}\n";
					}
				}
				break;

				case UnsolvabilityReason.SSS: {
					if (offs.Count == 1) {
						s += $"{offs0} is a point symmetric subspace of 10 cells\n";
					}
					else {
						s += $"One of {offss} is a point symmetric subspace of 10 cells\n";
					}
				}
				break;

				case UnsolvabilityReason.CannotCompleteString: {
					if (offs.Count == 1) {
						s += $"String {offs0} cannot be completed.\n";
					}
					else {
						s += $"One of the strings {offss} cannot be completed.\n";
					}
				}
				break;

				default:
					break;
			}
		}

		return s;
	}
}
