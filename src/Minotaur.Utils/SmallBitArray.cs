// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Edited 2022 by Sebastian Heuchler

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System;
using System.Text;
using System.IO;

namespace Minotaur.Utils;

/// <summary>
/// A vector of bits.
/// Use this to store bits efficiently.
/// </summary>
[Serializable]
public sealed class SmallBitArray :
	ICloneable,
	IEquatable<SmallBitArray>,
	IComparable<SmallBitArray>
{
	private uint [] _Array;
	private int _BitsLength;

	public SmallBitArray (BinaryReader br)
	{
		_BitsLength = br.ReadInt32 ();
		var n = (_BitsLength + 31) / 32;
		_Array = new uint [n];
		for (int i = 0; i < n; ++i) {
			_Array [i] = br.ReadUInt32 ();
		}
	}

	public SmallBitArray (uint [] data, int bitCount)
	{
		if ((bitCount + 31) / 32 != data.Length) {
			throw new ArgumentOutOfRangeException (nameof (bitCount));
		}
		_BitsLength = bitCount;
		_Array = data;
	}

	public void Write (BinaryWriter bw)
	{
		bw.Write ((Int32) _BitsLength);
		for (int i = 0; i < _Array.Length; i++) {
			bw.Write (_Array [i]);
		}
	}

	/*=========================================================================
	** Allocates space to hold length bit values. All of the values in the bit
	** array are set to defaultValue.
	**
	** Exceptions: ArgumentOutOfRangeException if length < 0.
	=========================================================================*/
	public SmallBitArray (int length, bool defaultValue = false)
	{
		if (length < 0) {
			throw new ArgumentOutOfRangeException (nameof (length));
		}

		_Array = new uint [GetInt32ArrayLengthFromBitLength (length)];
		_BitsLength = length;

		if (defaultValue) {
			Array.Fill (_Array, uint.MaxValue);

			// clear high bit values in the last int
			Div32Rem (length, out int extraBits);
			if (extraBits > 0) {
				_Array [^1] = (1u << extraBits) - 1;
			}
		}
	}

	/*=========================================================================
	** Allocates space to hold the bit values in bytes. bytes[0] represents
	** bits 0 - 7, bytes[1] represents bits 8 - 15, etc. The LSB of each byte
	** represents the lowest index value; bytes[0] & 1 represents bit 0,
	** bytes[0] & 2 represents bit 1, bytes[0] & 4 represents bit 2, etc.
	**
	** Exceptions: ArgumentException if bytes == null.
	=========================================================================*/
	public SmallBitArray (byte [] bytes)
	{
		// this value is chosen to prevent overflow when computing m_length.
		// m_length is of type int32 and is exposed as a property, so
		// type of m_length can't be changed to accommodate.
		if (bytes.Length > int.MaxValue / BitsPerByte) {
			throw new ArgumentException (nameof (bytes));
		}

		_Array = new uint [GetInt32ArrayLengthFromByteLength (bytes.Length)];
		_BitsLength = bytes.Length * BitsPerByte;

		uint totalCount = (uint) bytes.Length / 4;

		ReadOnlySpan<byte> byteSpan = bytes;
		for (int i = 0; i < totalCount; i++) {
			_Array [i] = BinaryPrimitives.ReadUInt32LittleEndian (byteSpan);
			byteSpan = byteSpan.Slice (4);
		}

		Debug.Assert (byteSpan.Length >= 0 && byteSpan.Length < 4);

		uint last = 0;
		switch (byteSpan.Length) {
			case 3:
				last = (uint) (byteSpan [2] << 16);
				goto case 2;
			// fall through
			case 2:
				last |= (uint) (byteSpan [1] << 8);
				goto case 1;
			// fall through
			case 1:
				_Array [totalCount] = last | byteSpan [0];
				break;
		}
	}

	public SmallBitArray (bool [] values)
	{
		_Array = new uint [GetInt32ArrayLengthFromBitLength (values.Length)];
		_BitsLength = values.Length;

		uint i = 0;
		for (; i < (uint) values.Length; i++) {
			if (values [i]) {
				int elementIndex = Div32Rem ((int) i, out int extraBits);
				_Array [elementIndex] |= 1u << extraBits;
			}
		}
	}

	/*=========================================================================
	** Allocates a new BitArray with the same length and bit values as bits.
	**
	** Exceptions: ArgumentException if bits == null.
	=========================================================================*/
	public SmallBitArray (SmallBitArray bits)
	{
		int arrayLength = GetInt32ArrayLengthFromBitLength (bits._BitsLength);
		_Array = new uint [arrayLength];
		Debug.Assert (bits._Array.Length == arrayLength);
		Array.Copy (bits._Array, _Array, arrayLength);
		_BitsLength = bits._BitsLength;
	}

	public bool this [int index] {
		get => Get (index);
		set => Set (index, value);
	}

	/*=========================================================================
	** Returns the bit value at position index.
	**
	** Exceptions: ArgumentOutOfRangeException if index < 0 or
	**             index >= GetLength().
	=========================================================================*/
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public bool Get (int index)
	{
		if ((uint) index >= (uint) _BitsLength) {
			ThrowArgumentOutOfRangeException (index);
		}

		return (_Array [index >> 5] & (1 << index)) != 0;
	}

	/*=========================================================================
	** Sets the bit value at position index to value.
	**
	** Exceptions: ArgumentOutOfRangeException if index < 0 or
	**             index >= GetLength().
	=========================================================================*/
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public void Set (int index, bool value)
	{
		if ((uint) index >= (uint) _BitsLength) {
			ThrowArgumentOutOfRangeException (index);
		}

		uint bitMask = 1u << index;
		ref uint segment = ref _Array [index >> 5];

		if (value) {
			segment |= bitMask;
		}
		else {
			segment &= ~bitMask;
		}
	}

	public static SmallBitArray CppToCsBitArray (ReadOnlySpan<char> convert)
	{
		var len = convert.Length;
		var ba = new SmallBitArray (len);
		var sy = 1;
		var at = 0;
		for (int i = len - 1; i >= 0; i--) {
			var c = convert [i];
			if (c == '0') {
				// No need to change default bit value
				++at;
			}
			else if (c == '1') {
				ba.Set (at, true);
				++at;
			}
			else {
				sy++;
			}
		}
		Debug.Assert (at <= ba.Length);
		ba.Length = at;
		//Debug.Assert (ba.Cs2CppBitarray (sy, len / sy) == new string (convert));
		return ba;
	}

	public string Cs2CppBitarray (int sy, int sx)
	{
		if (sx * sy != _BitsLength) {
			throw new ArgumentOutOfRangeException ("sx * sy != _BitsLength");
		}

		// Inverse order for C++ vs C# bitarrays
		var sb = new StringBuilder ((sx + 1) * sy);
		for (int y = sy - 1; y >= 0; y--) {
			for (int x = sx - 1; x >= 0; x--) {
				sb.Append (this [x + y * sx] ? '1' : '0');
			}
			if (y > 0) {
				sb.Append (',');
			}
		}
		return sb.ToString ();
	}

	public string Prefix (int nBits)
	{
		var sb = new StringBuilder (nBits);
		for (int i = 0; i < nBits; i++) {
			char b;
			var idx = _BitsLength - i - 1;
			if (idx >= 0) {
				b = this [idx] ? '1' : '0';
			}
			else {
				b = '0';
			}
			sb.Append (b);
		}
		var ret = sb.ToString ();
		Debug.Assert (ret.Length == nBits);
		return ret;
	}

	public bool [] ToBoolArray ()
	{
		var bs = new bool [_BitsLength];
		for (int i = _BitsLength; i >= 0; i--) {
			bs [i] = this [i];
		}
		return bs;
	}

	public int Length {
		get {
			return _BitsLength;
		}
		set {
			if (value < 0) {
				throw new ArgumentOutOfRangeException (nameof (value));
			}

			int newints = GetInt32ArrayLengthFromBitLength (value);
			if (newints != _Array.Length) {
				// grow or shrink (if wasting more than _ShrinkThreshold ints)
				Array.Resize (ref _Array, newints);
			}

			if (value > _BitsLength) {
				// clear high bit values in the last int
				Div32Rem (_BitsLength, out int bits);
				if (bits > 0) {
					_Array [^1] &= (1u << bits) - 1;
				}
			}

			_BitsLength = value;
		}
	}

	public int Count => _BitsLength;

	public object SyncRoot => this;

	public bool IsSynchronized => false;

	public bool IsReadOnly => false;

	public object Clone () => new SmallBitArray (this);

	// XPerY=n means that n Xs can be stored in 1 Y.
	private const int BitsPerInt32 = 32;
	private const int BitsPerByte = 8;

	private const int BitShiftPerInt32 = 5;
	private const int BitShiftPerByte = 3;
	private const int BitShiftForBytesPerInt32 = 2;

	/// <summary>
	/// Used for conversion between different representations of bit array.
	/// Returns (n + (32 - 1)) / 32, rearranged to avoid arithmetic overflow.
	/// For example, in the bit to int case, the straightforward calc would
	/// be (n + 31) / 32, but that would cause overflow. So instead it's
	/// rearranged to ((n - 1) / 32) + 1.
	/// Due to sign extension, we don't need to special case for n == 0, if we use
	/// bitwise operations (since ((n - 1) >> 5) + 1 = 0).
	/// This doesn't hold true for ((n - 1) / 32) + 1, which equals 1.
	///
	/// Usage:
	/// GetArrayLength(77): returns how many ints must be
	/// allocated to store 77 bits.
	/// </summary>
	/// <param name="n"></param>
	/// <returns>how many ints are required to store n bytes</returns>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	private static int GetInt32ArrayLengthFromBitLength (int n)
	{
		Debug.Assert (n >= 0);
		return (int) ((uint) (n - 1 + (1 << BitShiftPerInt32)) >> BitShiftPerInt32);
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	private static int GetInt32ArrayLengthFromByteLength (int n)
	{
		Debug.Assert (n >= 0);
		// Due to sign extension, we don't need to special case for n == 0, since ((n - 1) >> 2) + 1 = 0
		// This doesn't hold true for ((n - 1) / 4) + 1, which equals 1.
		return (int) ((uint) (n - 1 + (1 << BitShiftForBytesPerInt32)) >> BitShiftForBytesPerInt32);
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	private static int GetByteArrayLengthFromBitLength (int n)
	{
		Debug.Assert (n >= 0);
		// Due to sign extension, we don't need to special case for n == 0, since ((n - 1) >> 3) + 1 = 0
		// This doesn't hold true for ((n - 1) / 8) + 1, which equals 1.
		return (int) ((uint) (n - 1 + (1 << BitShiftPerByte)) >> BitShiftPerByte);
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	private static int Div32Rem (int number, out int remainder)
	{
		uint quotient = (uint) number / 32;
		remainder = number & (32 - 1);    // equivalent to number % 32, since 32 is a power of 2
		return (int) quotient;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	private static int Div4Rem (int number, out int remainder)
	{
		uint quotient = (uint) number / 4;
		remainder = number & (4 - 1);   // equivalent to number % 4, since 4 is a power of 2
		return (int) quotient;
	}

	private static void ThrowArgumentOutOfRangeException (int index)
	{
		throw new ArgumentOutOfRangeException (nameof (index), index, "ArgumentOutOfRange_IndexMustBeLess");
	}

	public bool Equals (SmallBitArray other)
	{
		if (ReferenceEquals (other, null)) {
			return false;
		}
		if (ReferenceEquals (this, other)) {
			return true;
		}
		if (_BitsLength != other.Length) {
			return false;
		}

		Debug.Assert (_Array.Length == other._Array.Length);
		var intsToCompare = _Array.Length;
		for (int i = 0; i < intsToCompare; i++) {
			if (_Array [i] != other._Array [i]) {
				return false;
			}
		}
		return true;
	}

	public override bool Equals (object obj)
	{
		Debug.Assert (obj is SmallBitArray); // That should not usually happen
		return Equals (obj as SmallBitArray);
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode ()
	{
		var h = new HashCode ();
		for (int i = 0; i < _Array.Length; i++) {
			h.Add (_Array [i]);
		}
		return h.ToHashCode ();
	}

	public override string ToString ()
	{
		var sb = new StringBuilder (_BitsLength);
		for (int i = 0; i < _BitsLength; i++) {
			sb.Append (this [i] ? '1' : '0');
		}
		return sb.ToString ();
	}

	public int CompareTo (SmallBitArray other)
	{
		if (_BitsLength != other._BitsLength) {
			throw new InvalidOperationException ("Comparing bit arrays of different size is not recommended.");
		}
		Debug.Assert (_Array.Length == other._Array.Length);
		for (int i = _Array.Length - 1; i >= 0; i--) {
			var cmp = _Array [i].CompareTo (other._Array [i]);
			if (cmp != 0) {
				//Debug.Assert (cmp == Legacy__CompareTo (other));
				return cmp;
			}
		}
		//Debug.Assert (0 == Legacy__CompareTo (other));
		return 0;
	}

	private int Legacy__CompareTo (SmallBitArray other)
	{
		if (_BitsLength != other._BitsLength) {
			throw new InvalidOperationException ("Comparing bit arrays of different size is not recommended.");
		}
		for (int i = _BitsLength - 1; i >= 0; --i) {
			if (this [i] && !other [i]) {
				return 1;
			}
			if (other [i] && !this [i]) {
				return -1;
			}
		}
		return 0;
	}

	public static bool operator == (SmallBitArray a, SmallBitArray b)
	{
		if (a is null) {
			return b is null;
		}
		else {
			return a.Equals (b);
		}
	}

	public static bool operator != (SmallBitArray a, SmallBitArray b)
	{
		return !(a == b);
	}
}

// todo 64 bit storage
