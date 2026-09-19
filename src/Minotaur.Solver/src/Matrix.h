#pragma once

#include <iostream>
#include <fstream>

#include "Coord.h"
#include "Grid.h"
#include "Piece.h"
#include "Cell.h"
#include "Stats.h"
#include "Global.h"



extern size_t maxSolutionsToPrint;

class Matrix {
public:
	const coord_t sy;
	const coord_t sx;
	ColHead* root;

private:
	void* const _Alloc;
	uintptr_t _NextAlloc;

public:
	size_t steps = 0;
	std::vector<Cell*> CurrentPath;
	size_t nSolutions = 0;

	inline ~Matrix() {
		/* not needed due to palcement new
		Cell* col = root;
		do {
			Cell* const next = col->R;
			col->DeleteColumn();
			col = next;
		} while (col != root);
		*/
		free(_Alloc);
	}

	Matrix(coord_t const sy, coord_t const sx, Grid const& grid);

	inline void Solve() {
		Solve(root);
	}

	void Solve(ColHead* const root);

	void PrintSelf() const;
};
