#pragma once

#include <vector>
#include <string>

#include "Coord.h"
#include "Global.h"



struct Piece
{
public:
	const char* const Name;
	Coord5s Covered;
	coord_t MX; // max x coord
	coord_t MY; // max y coord
	std::vector<Piece> Rotations;

	static Piece Create(const char* const name, const char* const sInit);

	Piece(const char* name, const Coord5s covered, const coord_t mx, const coord_t my);

	//needed? todo constexpr Piece(const Piece& clone) = default;

	inline bool equals(const Piece& other) const {
		if (MX != other.MX) {
			return false;
		}
		if (MY != other.MY) {
			return false;
		}
		for (size_t i = 0; i < 5; i++) {
			if (!other.Covered.contains(Covered.cs[i])) {
				return false;
			}
		}
		return true;
	}

	void print() const;
};

extern Piece const Ps[12];
