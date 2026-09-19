#include <iostream>

#include "Piece.h"



Piece const Ps[12]{
	Piece::Create("F", "11000,01100,01000"),
	Piece::Create("I", "11111,00000,00000"),
	Piece::Create("L", "11110,10000,00000"),
	Piece::Create("N", "11100,00110,00000"),
	Piece::Create("P", "11000,11000,10000"),
	Piece::Create("T", "11100,01000,01000"),
	Piece::Create("U", "11100,10100,00000"),
	Piece::Create("V", "11100,10000,10000"),
	Piece::Create("W", "11000,01100,00100"),
	Piece::Create("X", "01000,11100,01000"),
	Piece::Create("Y", "11110,01000,00000"),
	Piece::Create("Z", "11000,01000,01100"),
};

Piece Piece::Create(const char* const name, const char* const sInit) {
	Coord5s covered = { 0 };
	size_t n = 0;
	const char* pI = sInit;
	coord_t mx = 0;
	coord_t my = 0;
	for (coord_t y = 0; y < 3; y++) {
		for (coord_t x = 0; x < 5; x++) {
			if (*pI != '0') {
				covered.cs[n].x = x;
				covered.cs[n].y = y;
				if (x > mx) {
					mx = x;
				}
				if (y > my) {
					my = y;
				}
				++n;
			}
			++pI;
		}
		++pI;
	}
	return Piece(name, covered, mx, my);
}

Piece::Piece(
	const char* const name,
	const Coord5s covered,
	const coord_t mx,
	const coord_t my)
	:
	Name(name),
	Covered(covered),
	MX(mx),
	MY(my)
{
	/*
	* Create all unique rotations
	*/
	Piece rot(*this);

	for (size_t rotationIndex = 0; rotationIndex < 8; rotationIndex++) {
		auto& rotC = rot.Covered.cs;

		if (rotationIndex & 1) {
			// mirror_h
			for (size_t i = 0; i < 5; i++) {
				auto& c = rotC[i];
				c.x = rot.MX - c.x;
			}
		}
		else if (rotationIndex & 2) {
			// mirror_v
			for (size_t i = 0; i < 5; i++) {
				auto& c = rotC[i];
				c.y = rot.MY - c.y;
			}
		}
		else if (rotationIndex & 4) {
			// flip_diag
			for (size_t i = 0; i < 5; i++) {
				auto& c = rotC[i];
				std::swap(c.x, c.y);
				std::swap(rot.MX, rot.MY);
			}
		}

		bool found = false;
		for (const auto& old : Rotations) {
			if (old.equals(rot)) {
				found = true;
				break;
			}
		}
		if (!found) {
			Rotations.push_back(rot);
		}
	}

	//printf("Created piece %s\n", Name);
}

void Piece::print() const
{
	for (size_t i = 0; i < 5; i++) {
		std::cout << Covered.cs[i].x << "," << Covered.cs[i].y;
	}
	std::cout << "\n";

	for (coord_t y = 0; y <= MY; y++) {
		for (coord_t x = 0; x <= MX; x++) {
			Coord c;
			c.x = x;
			c.y = y;
			if (Covered.contains(c)) {
				printf("##");
			}
			else {
				printf("..");
			}
		}
		printf("\n");
	}
}
