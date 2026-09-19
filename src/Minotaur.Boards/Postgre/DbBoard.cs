using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Minotaur.Boards.Postgre;

[Index (nameof (SolutionCount), Name = "idx_boards_solutioncount")]
[Index (nameof (SX), nameof (SY), nameof (NPieces), nameof (SolutionCount), Name = "idx_boards_sx_sy_np_sc")]
[DataContract (Namespace = "Minotaur.Boards")]
public class DbBoard
{
	public DbBoard () { }

	public DbBoard (
		string invariantGridName,
		byte sx,
		byte sy,
		byte npieces,
		int solutionCount,
		//string json,
		string preview,
		int solutionSteps,
		byte version = 1)
	{
		InvariantGridName = invariantGridName ?? throw new ArgumentNullException (nameof (invariantGridName));
		SX = sx;
		SY = sy;
		NPieces = npieces;
		SolutionCount = solutionCount;
		//JSON = json;
		Preview = preview;
		SolutionSteps = solutionSteps;
		Version = version;
	}

	[DataMember]
	[Key]
	[Required]
	[MaxLength (255)]
	public string InvariantGridName { get; set; }

	[DataMember]
	[Required]
	[Range (1, 100)]
	public byte SX { get; set; }

	[DataMember]
	[Required]
	[Range (1, 100)]
	public byte SY { get; set; }

	[DataMember]
	[Required]
	public int SolutionCount { get; set; }

	// not required
	//public string JSON { get; set; }

	[DataMember]
	[MaxLength (255)]
	public string Preview { get; set; }

	[DataMember]
	[Required]
	public int SolutionSteps { get; set; }

	[DataMember]
	[Required]
	[Range (0, 12)]
	public byte NPieces { get; set; }

	[DataMember]
	[Required]
	public byte Version { get; set; }

	public override string ToString ()
	{
		return InvariantGridName;
	}
}
