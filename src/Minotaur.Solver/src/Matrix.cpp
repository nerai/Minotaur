#include<ranges>

#include "Matrix.h"
#include "GlobalOptions.h"



#define DBG_PRINT 0

static std::string CellNames[256];

size_t maxSolutionsToPrint = 1;

Matrix::Matrix(coord_t const sy, coord_t const sx, Grid const& grid) :
	sy(sy),
	sx(sx),
	root(0),
	_Alloc(malloc(
		sizeof(ColHead)* (
			1 + size_t(sx) * sy + 12
			)
		+
		sizeof(Cell) * (
			size_t(sx) * sy // Number of cells
			* 12 // Number of pieces
			* 8 // Rotations per piece
			* 5 // Nodes in matrix per row
			)
	)),
	_NextAlloc(uintptr_t(_Alloc))
{
	if (!_Alloc) {
		std::cout << "Failed to alloc memory for matrix, aborting\n";
		std::abort();
	}

	for (size_t i = 0; i < sizeof(CellNames) / sizeof(CellNames[0]); i++) {
		CellNames[i] = std::to_string(i);
	}

	std::vector<ColHead*> allCols;
	root = ColHead::AddColumnHead(
		_NextAlloc,
		true,
		"/");
	allCols.push_back(root);

	/*
	Create columns for cells of the board
	*/
	std::vector<ColHead*> cellLookup;
	cellLookup.resize(size_t(sy) * sx);
	for (coord_t y = 0; y < sy; y++) {
		for (coord_t x = 0; x < sx; x++) {
			const coord_t ic = y * sx + x;
			const bool g = grid.Data()[ic];
			if (g) {
				// Cell is occupied
				cellLookup[ic] = 0;
			}
			else {
				// Cell remains to be solved
#if DBG_PRINT
				printf("Inserting column for cell %d\n", ic);
#endif
				auto col = ColHead::AddColumnHead(
					_NextAlloc,
					false,
					CellNames[ic].c_str());
				cellLookup[ic] = col;
				allCols.push_back(col);
			}
		}
	}

	/*
	Create columns for pieces
	*/
	ColHead* pieceLookup[12];
	for (size_t ip = 0; ip < 12; ++ip) {
		const Piece& p = Ps[ip];
		auto col = ColHead::AddColumnHead(
			_NextAlloc,
			true,
			p.Name);
		pieceLookup[ip] = col;
		allCols.push_back(col);
	}

	/*
	* Connect all column heads L/R
	*/
	Cell* prev = allCols.back();
	for (auto cell : allCols) {
		if (cell == 0) {
			continue;
		}
		cell->L = prev;
		prev = cell;
	}
	prev = allCols.front();
	for (auto cell : std::ranges::reverse_view(allCols)) {
		if (cell == 0) {
			continue;
		}
		cell->R = prev;
		prev = cell;
	}

#if DBG_PRINT
	for (size_t i = 0; i < allCols.size(); i++) {
		_ASSERT(allCols[i]->R == allCols[(i + 1 + allCols.size()) % allCols.size()]);
		_ASSERT(allCols[i]->L == allCols[(i - 1 + allCols.size()) % allCols.size()]);
		_ASSERT(allCols[i]->U == allCols[i]);
		_ASSERT(allCols[i]->D == allCols[i]);
		_ASSERT(allCols[i]->Head == allCols[i]);
	}
#endif

	/*
	Iterate over pieces and their rotations
	*/
	rowIndex_t rowIndex = 0;
	for (size_t ip = 0; ip < 12; ++ip) {
		const Piece& p = Ps[ip];

		for (const auto& rot : p.Rotations) {
#if DBG_PRINT
			std::cout << "Inserting columns for piece " << rot.Name << "\n";
#endif
			/*
			* Iterate over all positions where the piece could be inserted
			*/
			for (coord_t y0 = 0; y0 < grid.SY(); y0++) {
				for (coord_t x0 = 0; x0 < grid.SX(); x0++) {
					coord_t inserts[5];
					bool fail = false;

					for (size_t i = 0; i < 5; ++i) {
						Coord c(rot.Covered.cs[i]);
						c.x += x0;
						c.y += y0;
						if (c.x >= grid.SX() || c.y >= grid.SY()) {
							fail = true;
							break;
						}

						const coord_t& ic = c.y * grid.SX() + c.x;
						const bool g = grid.Data()[ic];
						if (g) {
							fail = true;
							break;
						}

						inserts[i] = ic;
					}

					if (fail) {
						continue;
					}

					/*
					insert row into matrix
					*/
					++rowIndex;
					Cell* const row = pieceLookup[ip]->CreateRowNode(
						_NextAlloc,
						rowIndex);
#if DBG_PRINT
					std::cout << "add row " << row->str();
#endif
					Cell* pivot = row;
					for (size_t i = 0; i < 5; i++) {
						const size_t col = inserts[i];
						Cell* const add = cellLookup[col]->CreateRowNode(
							_NextAlloc,
							rowIndex);
						add->L = pivot;
						pivot->R = add;
						pivot = add;
#if DBG_PRINT
						std::cout << ", " << col << " " << add->str();
#endif
					}
#if DBG_PRINT
					std::cout << " for piece " << p.Name.c_str() << "\n";
#endif
					row->L = pivot;
					pivot->R = row;
				}
			}
		}
	}

#if DBG_PRINT
	std::cout << "Matrix has " << rowIndex << " rows\n";
	PrintSelf();
#endif
}

void Matrix::Solve(ColHead* const root) {
	if (0) {
		std::cout << steps << ": ";
		for (const auto& cell : CurrentPath) {
			std::cout << cell->Head->What << ", ";
		}
		std::cout << "\n";
	}

	if (root->hasOnlyOptionalColumns()) {
		nSolutions++;
		if (nSolutions <= maxSolutionsToPrint) {
			std::cout << "Solution";
			if (verbose) {
				std::cout << " (" << steps << " steps):";
				for (const auto pCell : CurrentPath) {
					pCell->printRow();
					std::cout << ";";
				}
			}
			else if (binaryOutput) {
				// todo
			}
			else {
				for (const auto pCell : CurrentPath) {
					pCell->printRow();
					std::cout << ";";
				}
			}
			std::cout << "\n";
		}
		return;
	}

	if (focusUniq && nSolutions >= 2) {
		return;
	}

	++steps;

	ColHead* col = root->findShortestColumn();

	/*
	 * As the pivot column is, by definition, solved, we can remove it immediately
	 */
	col->Detach();

	/*
	 * The vertical nodes in the column are the options that we try.
	 * We don't know which ones will work.
	 */
	Cell* vertical = col->D;
	while (vertical != col) {
		_ASSERT(vertical->Head == col);

		/*
		 * This option covers some columns, so we remove these.
		 */
		Cell* horizontal = vertical->R;
		while (horizontal != vertical) {
			horizontal->Head->Detach();
			horizontal = horizontal->R;
		}

		/*
		 * Recurse
		 */
		CurrentPath.push_back(vertical);
		Solve(root);
		CurrentPath.pop_back();

		/*
		 * Restore everything
		 */
		horizontal = vertical->L;
		while (horizontal != vertical) {
			horizontal->Head->Reattach();
			horizontal = horizontal->L;
		}

		/*
		 * Go to next option (= row)
		 */
		vertical = vertical->D;
	}

	/*
	 * Restore the pivot column
	 */
	col->Reattach();
}

void Matrix::PrintSelf() const {
	{
		Cell const* col = root;
		do {
			col = col->R;
			std::cout << col->Head->What << " ";
		} while (col != root);
		std::cout << "\n";

		col = root;
		do {
			col = col->R;
			std::cout << col->Head->RowCount << " ";
		} while (col != root);
		std::cout << "\n";
	}

	Cell const* pivot = root;
	for (size_t i = 0; i < 12; i++) {
		pivot = pivot->L;

		Cell const* row = pivot->D;
		while (row != pivot) {
			Cell const* col = root;
			do {
				col = col->R;

				bool contains = 0;
				Cell const* horiz = row;
				do {
					contains |= col == horiz->Head;
					horiz = horiz->R;
				} while (horiz != row);

				if (contains) {
					std::cout << col->Head->What;
				}
				else {
					std::cout << ".";
					for (size_t i = 1; col->Head->What[i] != 0; i++) {
						std::cout << " ";
					}
				}
				std::cout << " ";
			} while (col != root);

			std::cout << "\n";
			row = row->D;
		};
	}
}
