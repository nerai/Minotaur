#include <iostream>

#include "Grid.h"



Grid::Grid(std::string const& sGrid)
{
	if (sGrid.empty()) {
		std::cout << "No grid specified.\n";
		exit(3);
	}

	auto comma = sGrid.find(',');
	if (comma == std::string::npos) {
		comma = sGrid.size();
	}
	_sx = coord_t(comma);
	_sy = coord_t((sGrid.size() + 1) / (size_t(_sx) + 1));
	if (false
		|| _sx > 14
		|| _sy > 14
		|| size_t(_sy) * (size_t(_sx) + 1) - 1 != sGrid.size()
		) {
		std::cout << "Grid size could not be recognized, quitting.";
		std::cout << " sy " << (int)_sy << " * (sx " << (int)_sx << " + 1) - 1 != " << sGrid.size() << "\n";
		std::cout << "In: " << sGrid << "\n";
		exit(2);
	}

	/*
	Build grid
	*/
	_grid.resize(size_t(_sx) * _sy, 0);
	size_t i = 0;
	for (const auto c : sGrid) {
		if (c == ',') {
			continue;
		}
		_grid[i] = c != '0';
		++i;
	}
}

const bool DEBUG_PRINT_GRID_MINIMUM = 0;

bool Grid::isGridMinimum() const {
	size_t n = size_t(_sx) * _sy;
	std::vector<bool> buf = _grid;
	auto const MY = _sy - 1;
	auto const MX = _sx - 1;

	for (size_t rotationIndex = 1; rotationIndex < 8; rotationIndex++) {
		if (rotationIndex >= 4 && (_sx != _sy)) {
			continue;
		}

		for (coord_t y0 = 0; y0 < _sy; y0++) {
			for (coord_t x0 = 0; x0 < _sx; x0++) {
				coord_t ey = y0;
				coord_t ex = x0;
				coord_t esx = _sx;

				if (rotationIndex & 1) {
					// mirror_h
					ex = MX - ex;
				}

				if (rotationIndex & 2) {
					// mirror_v
					ey = MY - ey;
				}

				if (rotationIndex & 4) {
					// flip_diag
					std::swap(ex, ey);
					esx = _sy;
				}

				buf[ex + ey * esx] = _grid[x0 + y0 * _sx];
			}
		}

		if (DEBUG_PRINT_GRID_MINIMUM) {
			std::cout << "Rotation " << rotationIndex << "\n";
			for (size_t y = 0; y < _sy; y++) {
				for (size_t x = 0; x < _sx; x++) {
					auto const c = buf[x + y * _sx];
					std::cout << (int)c << " ";
				}
				std::cout << "\n";
			}
		}

		for (size_t i = 0; i < n; i++) {
			auto const actual = _grid[i];
			auto const rotated = buf[i];
			if (DEBUG_PRINT_GRID_MINIMUM) {
				std::cout << "[" << actual << "/" << rotated << "]";
			}
			if (actual && !rotated) {
				return false;
			}
			if (rotated && !actual) {
				break;
			}
		}
	}

	return true;
}
