#pragma once

#include <cstdint>

#include "Global.h"



typedef uint8_t coord_t;

struct Coord {
public:
	coord_t x;
	coord_t y;

	inline bool equals(const Coord& c) const {
		return x == c.x && y == c.y;
	}
};

struct Coord5s {
public:
	Coord cs[5];

	inline bool contains(const Coord& c) const {
		for (size_t i = 0; i < 5; i++) {
			if (cs[i].equals(c)) {
				return true;
			}
		}
		return false;
	}
};
