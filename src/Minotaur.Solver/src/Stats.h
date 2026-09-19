#pragma once

#include <cstdint>
#include <string>

#include "Global.h"



class Stats {
public:
	char const* const programVersion = "PentominoCPP  (build " __DATE__ ", " __TIME__ ")";

	uint64_t timeStart;
	double timeTotalSeconds;

	std::string board0; // Inclusive start of range
	std::string board1; // Inclusive end of range
//	std::string board2; // Start of following range

	uint64_t permuted = 0;
	uint64_t canonical = 0;

	uint64_t unsolvableN = 0;
	uint64_t unsolvableSumSteps = 0;
	uint64_t unsolvableMaxSteps = 0;

	uint64_t solvableN = 0;
	uint64_t solvableSumSteps = 0;
	uint64_t solvableMaxSteps = 0;
	uint64_t solvableSumSolutions = 0;
	uint64_t solvableMaxSolutions = 0;

	uint64_t uniqN = 0;
	uint64_t uniqSumSteps = 0;
	uint64_t uniqMaxSteps = 0;
};
