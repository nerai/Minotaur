#pragma once

#include <string>
#include <cstdint>
#include <memory>

#include "Global.h"



// For debugging. About 8% slower.
#define USE_ROWINDEX 0

typedef uint16_t rowIndex_t;

struct Cell;
struct ColHead;

struct Cell
{
public:
#if USE_ROWINDEX
	rowIndex_t const RowIndex;
#endif
	ColHead* const Head;
	Cell* L;
	Cell* R;
	Cell* U;
	Cell* D;

#if USE_ROWINDEX
	inline Cell(ColHead* const head, rowIndex_t const rowIndex) :
		RowIndex(rowIndex),
#else
	inline Cell(ColHead* const head) :
#endif
		Head(head),
		L(this),
		R(this),
		U(this),
		D(this)
	{
	}

	void printRow() const;
	void printRow_Debug() const;
	std::string str() const;

	/* Not needed due to placement new
	inline void DeleteColumn() {
		const Cell* row = this;
		do {
			const Cell* next = row->D;
			row->~Cell();
			row = next;
		} while (row != this);
	}
	*/
};

struct ColHead : public Cell
{
public:
	rowIndex_t RowCount;
	uint8_t const IsOptional;
	char const* const What;

	inline ColHead(
		bool const optional,
		char const* const what) :
#if USE_ROWINDEX
		Cell(this, 0),
#else
		Cell(this),
#endif
		RowCount(0),
		IsOptional(optional),
		What(what)
	{
	}

	static ColHead* AddColumnHead(
		uintptr_t& refPlacement,
		bool const optional,
		char const* const what);
	Cell* CreateRowNode(
		uintptr_t& refPlacement,
		rowIndex_t const rowIndex);

	/// <summary>
	/// Detach this row.
	/// Also detach all rows linked to it, since none of them can be used anymore when this column is blocked.
	/// </summary>
	void Detach();

	void Reattach();

	bool hasOnlyOptionalColumns() const;

	ColHead* findShortestColumn() const;
};
