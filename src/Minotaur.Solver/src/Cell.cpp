#include <iostream>

#include "Cell.h"



#ifndef ASSUME_ROW_HAS_6_ELEMENTS
#define ASSUME_ROW_HAS_6_ELEMENTS 1
#endif
#ifndef UNROLL_COVER_LOOP
#define UNROLL_COVER_LOOP 1
#endif

/* todo
void Cell::printRow_Debug() const {
	const Cell* col = this;
	do {
		std::cout << " " << col->str();
		col = col->R;
	} while (col != this);
	printf("\n");
}
*/

void Cell::printRow() const {
	const Cell* horiz = this;
	do {
		std::cout << " " << horiz->Head->What;
		horiz = horiz->R;
	} while (horiz != this);
}

std::string Cell::str() const {
	std::string s;
	s += Head->What;
	if (Head->IsOptional) {
		s += "?";
	}
	s += "/";
#if USE_ROWINDEX
	s += std::to_string(RowIndex);
#endif
	/*
	if (Head != this) {
		s += " (";
		s += Head->str();
		s += ")";
	}
	*/
	return s;
}

ColHead* asHead(Cell* pCell) {
	_ASSERT(pCell->Head == pCell);
	return static_cast<ColHead*> (pCell);
}

bool ColHead::hasOnlyOptionalColumns() const {
	const ColHead* col = asHead(R);
	while (col != this) {
		if (!col->IsOptional) {
			return false;
		}
		col = asHead(col->R);
	}
	return true;
}

ColHead* ColHead::findShortestColumn() const {
	ColHead* min_col(0);
	rowIndex_t min_n(-1);
	ColHead* col = asHead(R);
	while (col != this) {
		if ((col->RowCount < min_n) && (!col->IsOptional)) {
			min_n = col->RowCount;
			min_col = col;
		}
		col = asHead(col->R);
	}
	return min_col;
}

ColHead* ColHead::AddColumnHead(
	uintptr_t& refPlacement,
	bool const optional,
	char const* const what)
{
	const auto placement = (void*)refPlacement;

	ColHead* newCol = new (placement)ColHead(
		optional,
		what);

	refPlacement += sizeof(ColHead);
	return newCol;
}

Cell* ColHead::CreateRowNode(
	uintptr_t& refPlacement,
	rowIndex_t const rowIndex)
{
	const auto placement = (void*)refPlacement;

	Cell* node = new (placement) Cell(
		this
#if USE_ROWINDEX
		, rowIndex
#endif
	);

	node->D = this;
	node->U = U;
	U->D = node;
	U = node;
	RowCount += 1;

#if DEBUG
	if (refPlacement + sizeof(Cell) < refPlacement) {
		std::cout << "CRITICAL ERROR: overflow of placement\n";
		exit(1);
	}
#endif

	refPlacement += sizeof(Cell);
	return node;
}

void ColHead::Detach()
{
	R->L = L;
	L->R = R;

	Cell* row = D;
	while (row != this) {
		Cell* col = row->R;
#if !UNROLL_COVER_LOOP
#if ASSUME_ROW_HAS_6_ELEMENTS
		// It was measured that the automatic unroll is about 5% slower than the manual unroll
		#pragma GCC unroll 999
		for (size_t i = 0; i < 5; i++) {
#else
		while (col != row) {
#endif
			col->U->D = col->D;
			col->D->U = col->U;
			col->Head->RowCount -= 1;
			col = col->R;
		}
#else
		// 0-
		auto c0 = col;

		// 0a
		auto c1 = c0->R;
		__assume(c0 != c1);
		auto u0 = c0->U;
		auto d0 = c0->D;
		auto h0 = c0->Head;

		// 1a
		auto c2 = c1->R;
		__assume(c0 != c2);
		__assume(c1 != c2);
		auto u1 = c1->U;
		__assume(u0 != u1);
		auto d1 = c1->D;
		__assume(d0 != d1);
		auto h1 = c1->Head;
		__assume(h0 != h1);

		// 0b
		__assume(d0->U != d0);
		__assume(d0->U != u0);
		__assume(d0->U != u0->D);
		d0->U = u0;
		u0->D = d0;
		h0->RowCount -= 1;

		// 2a
		auto c3 = c2->R;
		__assume(c0 != c3);
		__assume(c1 != c3);
		__assume(c2 != c3);
		auto u2 = c2->U;
		__assume(u0 != u2);
		__assume(u1 != u2);
		auto d2 = c2->D;
		__assume(d0 != d2);
		__assume(d1 != d2);
		auto h2 = c2->Head;
		__assume(h0 != h2);
		__assume(h1 != h2);

		// 1b
		__assume(d1->U != d1);
		__assume(d1->U != u1);
		__assume(d1->U != u1->D);
		d1->U = u1;
		u1->D = d1;
		h1->RowCount -= 1;

		// 3a
		auto c4 = c3->R;
		__assume(c0 != c4);
		__assume(c1 != c4);
		__assume(c2 != c4);
		__assume(c3 != c4);
		auto u3 = c3->U;
		__assume(u0 != u3);
		__assume(u1 != u3);
		__assume(u2 != u3);
		auto d3 = c3->D;
		__assume(d0 != d3);
		__assume(d1 != d3);
		__assume(d2 != d3);
		auto h3 = c3->Head;
		__assume(h0 != h3);
		__assume(h1 != h3);
		__assume(h2 != h3);

		// 2b
		__assume(d2->U != d2);
		__assume(d2->U != u2);
		__assume(d2->U != u2->D);
		d2->U = u2;
		u2->D = d2;
		h2->RowCount -= 1;

		// 4a
		auto u4 = c4->U;
		__assume(u0 != u4);
		__assume(u1 != u4);
		__assume(u2 != u4);
		__assume(u3 != u4);
		auto d4 = c4->D;
		__assume(d0 != d4);
		__assume(d1 != d4);
		__assume(d2 != d4);
		__assume(d3 != d4);
		auto h4 = c4->Head;
		__assume(h0 != h4);
		__assume(h1 != h4);
		__assume(h2 != h4);
		__assume(h3 != h4);

		// 3b
		__assume(d3->U != d3);
		__assume(d3->U != u3);
		__assume(d3->U != u3->D);
		d3->U = u3;
		u3->D = d3;
		h3->RowCount -= 1;

		// 4b
		__assume(d4->U != d4);
		__assume(d4->U != u4);
		__assume(d4->U != u4->D);
		d4->U = u4;
		u4->D = d4;
		h4->RowCount -= 1;

#endif
		row = row->D;
	}
}

void ColHead::Reattach()
{
	Cell* row = D;
	while (row != this) {
		Cell* col = row->L;
#if !UNROLL_COVER_LOOP
#if ASSUME_ROW_HAS_6_ELEMENTS
		for (size_t i = 0; i < 5; i++) {
#else
		while (col != row) {
#endif
			col->U->D = col;
			col->D->U = col;
			col->Head->RowCount += 1;
			col = col->L;
		}
#else
		// 0-
		auto c0 = col;

		// 0a
		auto c1 = c0->L;
		__assume(c0 != c1);
		auto u0 = c0->U;
		auto d0 = c0->D;
		auto h0 = c0->Head;

		// 1a
		auto c2 = c1->L;
		__assume(c0 != c2);
		__assume(c1 != c2);
		auto u1 = c1->U;
		__assume(u0 != u1);
		auto d1 = c1->D;
		__assume(d0 != d1);
		auto h1 = c1->Head;
		__assume(h0 != h1);

		// 0b
		__assume(d0->U != c0);
		__assume(d0->U != u0);
		__assume(d0->U != u0->D);
		d0->U = c0;
		u0->D = c0;
		h0->RowCount += 1;

		// 2a
		auto c3 = c2->L;
		__assume(c0 != c3);
		__assume(c1 != c3);
		__assume(c2 != c3);
		auto u2 = c2->U;
		__assume(u0 != u2);
		__assume(u1 != u2);
		auto d2 = c2->D;
		__assume(d0 != d2);
		__assume(d1 != d2);
		auto h2 = c2->Head;
		__assume(h0 != h2);
		__assume(h1 != h2);

		// 1b
		__assume(d1->U != c1);
		__assume(d1->U != u1);
		__assume(d1->U != u1->D);
		d1->U = c1;
		u1->D = c1;
		h1->RowCount += 1;

		// 3a
		auto c4 = c3->L;
		__assume(c0 != c4);
		__assume(c1 != c4);
		__assume(c2 != c4);
		__assume(c3 != c4);
		auto u3 = c3->U;
		__assume(u0 != u3);
		__assume(u1 != u3);
		__assume(u2 != u3);
		auto d3 = c3->D;
		__assume(d0 != d3);
		__assume(d1 != d3);
		__assume(d2 != d3);
		auto h3 = c3->Head;
		__assume(h0 != h3);
		__assume(h1 != h3);
		__assume(h2 != h3);

		// 2b
		__assume(d2->U != c2);
		__assume(d2->U != u2);
		__assume(d2->U != u2->D);
		d2->U = c2;
		u2->D = c2;
		h2->RowCount += 1;

		// 4a
		auto u4 = c4->U;
		__assume(u0 != u4);
		__assume(u1 != u4);
		__assume(u2 != u4);
		__assume(u3 != u4);
		auto d4 = c4->D;
		__assume(d0 != d4);
		__assume(d1 != d4);
		__assume(d2 != d4);
		__assume(d3 != d4);
		auto h4 = c4->Head;
		__assume(h0 != h4);
		__assume(h1 != h4);
		__assume(h2 != h4);
		__assume(h3 != h4);

		// 3b
		__assume(d3->U != c3);
		__assume(d3->U != u3);
		__assume(d3->U != u3->D);
		d3->U = c3;
		u3->D = c3;
		h3->RowCount += 1;

		// 4b
		__assume(d4->U != c4);
		__assume(d4->U != u4);
		__assume(d4->U != u4->D);
		d4->U = c4;
		u4->D = c4;
		h4->RowCount += 1;
#endif
		row = row->D;
	}

	R->L = this;
	L->R = this;
}
