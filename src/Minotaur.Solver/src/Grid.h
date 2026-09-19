#pragma once

#include <string>
#include <vector>
#include <algorithm>
#include <sstream>

#include "Coord.h"
#include "Global.h"



class Grid {
public:
	inline Grid(const Grid& from) :
		_grid(from._grid),
		_sy(from._sy),
		_sx(from._sx)
	{}
	Grid(std::string const& sGrid);
	Grid(std::vector<bool> grid, coord_t sy, coord_t sx);

	inline coord_t SY() const { return _sy; }
	inline coord_t SX() const { return _sx; }
	inline std::vector<bool> const& Data() const { return _grid; }

	bool isGridMinimum() const;

	inline bool nextPermutation() {
		return std::next_permutation(begin(_grid), end(_grid));
	}
	inline bool prevPermutation() {
		return std::prev_permutation(begin(_grid), end(_grid));
	}

	inline std::string toString(std::string newline) const {
		std::stringstream ss;
		for (size_t y = 0; y < _sy; y++) {
			if (y > 0) {
				ss << newline;
			}
			for (size_t x = 0; x < _sx; x++) {
				auto const b = _grid[x + y * _sx];
				ss << (b ? 1 : 0);
			}
		}
		return ss.str();
	}

	// TODO stream write as binary (in bytes)

	inline std::string idString() const {
		std::string id;
		id += std::to_string(_sy);
		id += "x";
		id += std::to_string(_sx);
		id += ",";
		id += toString(",");
		return id;
	}

private:
	std::vector<bool> _grid;
	coord_t _sy;
	coord_t _sx;
};
