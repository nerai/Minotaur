#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>
#include <time.h>
#include <vector>
#include <cstdint>
#include <string>
#include <map>
#include <sstream>
#include <iostream>
#include <chrono>
#include <set>
#include <fstream>
#include <queue>
#include <bitset>
#include <algorithm>
#include <string_view>
#include <thread>
#include <mutex>
#include <queue>
#include <regex>

#ifdef __linux__
#include <pthread.h>
#include <sys/prctl.h>
#endif

#include "Util.h"
#include "Coord.h"
#include "Grid.h"
#include "Piece.h"
#include "Cell.h"
#include "Stats.h"
#include "Matrix.h"
#include "GlobalOptions.h"
#include "Semaphore.h"
#include "PentominoCPP.h"



void printHelp() {
	printf("PentominoCPP  (build " __DATE__ ", " __TIME__ ")");
	std::cout << "  Sebastian Heuchler 2022-2026\n";
	std::cout << "\n";
	std::cout << "Usage: PentominoCPP [--help] [--verbose | --binary-output] [--grid GRID]\n";
	std::cout << "\n";
	std::cout << "--help        Display this help text\n";
	std::cout << "\n";
	std::cout << "--verbose     Print lots of human readable information\n";
	std::cout << "\n";
	std::cout << "--binary-output  Print results in binary, and do not print solutions\n";
	std::cout << "\n";
	std::cout << "--grid GRID   Set open and closed cells.\n";
	std::cout << "              GRID is a continuous string of 0s (open) and 1s (closed) cells, rows separated by a comma\n";
	std::cout << "\n";
	std::cout << "Example:\n";
	std::cout << "< PentominoCPP --size 4 5 --grid 00000000001001011010\n";
	std::cout << "\n";
	std::cout << "> PentominoCPP v0.1\n";
	std::cout << "> ID 4x5,00000,00000,10010,11010,\n";
	std::cout << "> Solution 19 3 L 4 9 14; 17 F 8 7 12 11; 0 5 1 6 2 P;\n";
	std::cout << "> SolCount 6\n";
	std::cout << "> Steps 30\n";
	std::cout << "> Time 0.001649s\n";
	std::cout << "\n";
}

void doGrid(Grid const& grid, Matrix const** pResult)
{
	/*
	 * Parse grid
	 */
	std::string id;
	if (!cumulative) {
		if (verbose) {
			if (!binaryOutput) {
				id = grid.idString();
				std::cout << "ID " << id << "\n";
			}
		}
	}

	auto t0 = std::chrono::steady_clock::now();

	auto mm = new Matrix(grid.SY(), grid.SX(), grid);

	if (verbose) {
		std::cout << "Matrix built at " << timeFromD(t0) << "s.\n";
	}

	/*
	std::map<size_t, const Cell*> allRows;
	for (size_t ip = 0; ip < 12; ++ip) {
		const ColHead* const piece = pieceLookup[ip];
		const Cell* walk = piece->D;
		while (walk != piece) {
			if (!allRows.count(walk->RowIndex)) {
				allRows[walk->RowIndex] = walk;
				//walk->printRow();
			}
			walk = walk->D;
		}
	}
	*/

	/*
	Preprocessing:
	Hide all columns that are implied by other columns, e.g.
	  1 2 3 4
	A 1 1 0 1
	B 1 0 1 1
	C 0 1 1 0
	In this case, col 4 is already defined by the choice in col 1.
	*/
	/* TODO
	std::cout << "Preprocessing...\n";
	for (size_t icol = 0; icol < sx * sy; icol++) {
		ColHead* col = cellLookup[icol];
		if (col == 0) {
			continue;
		}
		_ASSERT(!col->IsOptional); // this is guaranteed for Pentomino boards
		std::cout << "Pivot column " << col->What;

		Cell* vert = col->D;
		std::map<ColHead*, size_t> reached;
		size_t n = 0;
		while (vert != col) {
			++n;
			//std::cout << "Row " << vert->str() << ":";

			Cell* horiz = vert->R;
			while (horiz != vert) {
				//std::cout << " " << horiz->str();
				reached[horiz->Head] += 1; // Increment, if key missing prepare with 0.
				horiz = horiz->R;
			}
			vert = vert->D;
			//std::cout << "\n";
		}

		std::cout << " =>";
		for (auto const& kv : reached) {
			if (kv.first->RowCount <= kv.second) {
				std::cout << " contains " << kv.first->What << " (" << kv.second << "/" << kv.first->RowCount << ");";
			}
			if (kv.second >= n) {
				std::cout << " forces " << kv.first->What << " (" << kv.second << "/" << kv.first->RowCount << ");";
			}
		}
		std::cout << "\n";
	}
	std::cout << "Preprocessing completed at " << timeFrom(t0) << ".\n";
	//*/

	mm->Solve();

	if (!cumulative) {
		if (verbose) {
			std::cout << "Found " << mm->nSolutions << " solutions after " << mm->steps << " steps in " << timeFromD(t0) << "s.\n";
		}
		else if (binaryOutput) {
			std::cout.write((char*)(mm->nSolutions), sizeof(mm->nSolutions));
			std::cout.write((char*)(mm->steps), sizeof(mm->steps));
		}
		else {
			/*
			std::cout << "SolCount " << mm->nSolutions << "\n";
			std::cout << "Steps " << mm->steps << "\n";
			std::cout << "Time " << timeFromD(t0) << "s\n";
			*/
			if (focusUniq) {
				if (mm->nSolutions == 1) {
					std::cout << mm->steps << "\n";
				}
			}
			else {
				std::cout << timeFromD(t0) << "\t";
				std::cout << mm->nSolutions << "\t";
				std::cout << mm->steps << "\t";
				std::cout << grid.idString() << "\n";
			}
		}
	}

	if (pResult) {
		*pResult = mm;
	}
	else {
		delete mm;
	}
}

void addResultToStats(Matrix const& mm, Stats* stats) {
	if (mm.nSolutions == 0) {
		stats->unsolvableN += 1;
		stats->unsolvableSumSteps += mm.steps;
		if (mm.steps > stats->unsolvableMaxSteps) {
			stats->unsolvableMaxSteps = mm.steps;
		}
	}
	else {
		stats->solvableN += 1;
		stats->solvableSumSteps += mm.steps;
		if (mm.steps > stats->solvableMaxSteps) {
			stats->solvableMaxSteps = mm.steps;
		}

		stats->solvableSumSolutions += mm.nSolutions;
		if (mm.nSolutions > stats->solvableMaxSolutions) {
			stats->solvableMaxSolutions = mm.nSolutions;
		}
		if (mm.nSolutions == 1) {
			stats->uniqN += 1;
			stats->uniqSumSteps += mm.steps;
			if (mm.steps > stats->uniqMaxSteps) {
				stats->uniqMaxSteps = mm.steps;
			}
		}
	}
}

#if defined _DEBUG && 0
#define DBG(x) std::cout << x << "\n"
#else
#define DBG(x) ((void)0)
#endif

int main(int argc, char* argv[])
{
#ifdef _DEBUG
	std::cout << "[DEBUG MODE]\n";
#endif

	const size_t maxThreads = std::thread::hardware_concurrency();
	std::string outfile = "";

#ifdef __linux__
	auto myName = system_exec("whoami");
	myName.pop_back(); // Remove trailing newline
#endif

	Semaphore semActive(1);
	volatile bool please_end = false;
	double targetTempDaytime = 75.0;

	/*
	 * Prepare concurrent queue
	 */
	Semaphore workAvailable(0);
	Semaphore spaceAvailable(0);
	std::mutex mWork;
	std::mutex mStats;
	std::queue<Grid const*> work;
	std::vector<std::thread> threads;
	Stats* pStats = 0;

	/*
	 * Start threads
	 */
	for (size_t i = 0; i < maxThreads; i++) {
		const size_t tid(i);
		threads.emplace_back(std::thread(
			[
				tid,
				&workAvailable,
				&spaceAvailable,
				&mWork,
				&mStats,
				&work,
				&pStats,
				&semActive
			] {
				DBG("Start thread " << tid);
#ifdef __linux__
				struct sched_param sparam;
				sparam.sched_priority = 0;
				pthread_setschedparam(pthread_self(), SCHED_IDLE, &sparam);
				std::string threadName = "Work ";
				threadName += std::to_string(tid);
				prctl(PR_SET_NAME, threadName.c_str(), 0, 0, 0);
#endif

				while (1) {
					DBG("thread " << tid << " looking for work");
					workAvailable.acquire();
					Grid const* pLocalGrid;
					{
						std::unique_lock<std::mutex> mlock(mWork);
						if (work.size() != 0) {
							pLocalGrid = work.front();
							work.pop();
						}
						else {
							// Program is terminating
							return;
						}
					}
					DBG("thread " << tid << " got work " << pLocalGrid);

					// Check if we're restricted by thermals
					semActive.acquire();
					semActive.release();

					if (pLocalGrid->isGridMinimum()) {
						Matrix const* pResult = 0;
						doGrid(*pLocalGrid, &pResult);
						{
							DBG("thread " << tid << " finished work " << pLocalGrid);
							std::unique_lock<std::mutex> mlock(mStats);
							++(pStats->canonical);
							addResultToStats(*pResult, pStats);
						}
						delete pResult;
					}
					delete pLocalGrid;

					spaceAvailable.release();
				}
				DBG("thread " << tid << " END");
			}));
	}

#ifdef __linux__
	std::thread threadTemp(
		[
			&myName,
			&please_end,
			maxThreads,
			&semActive,
			&targetTempDaytime
		] {
			prctl(PR_SET_NAME, "Thermal", 0, 0, 0);

			bool open = true;
			double suspend = 0;
			double ratio = 0;
			std::regex rexSensorsTemp("^[^\\.]*\\+([0-9\\.]+).*", std::regex_constants::optimize);

			while (!please_end) {
				/*
				* The update interval is an important parameter.
				* It should be low, to react quickly when the temperature changes.
				* It should also be high, to avoid stalling most threads but waiting for a single
				* thread to finish its long task, which causes CPU turbo.
				* Further, it also influences if the CPU fan spins up/down often or stays at a stable speed.
				*/
				std::this_thread::sleep_for(std::chrono::milliseconds(100));

				double targetTemp;
				std::string sensors;
				time_t now = time(NULL);
				struct tm* now_tm = localtime(&now);

				if (myName == "my_local_username_instead_of_the_UPB_computers") {
					// debian
					sensors = system_exec("sensors | grep 'Tdie'");
					targetTemp = 60;
				}
				else {
					// UPB
					/*
					* CPU fan speeds
					* T[C]; Fan 1 rpm; Fan 2 rpm;
					*  70 ;  800- 820;  950-1150;
					*  72 ;  800- 820;  960-1400;
					*/
					int hour = now_tm->tm_hour;
					if (hour < 7 || hour >= 22 || now_tm->tm_wday == 0 || now_tm->tm_wday == 6) {
						targetTemp = 999;
					}
					else {
						sensors = system_exec("sensors | grep 'Package id 0'");
						targetTemp = targetTempDaytime;
					}
				}

				if (targetTemp < 100) {
					double curTemp = 0;
					std::smatch m;
					if (std::regex_search(sensors, m, rexSensorsTemp)) {
						curTemp = std::stod(m[1].str());
					}
					else {
						std::cout << "Failed to parse sensors output: " << sensors << "\n";
					}

					if (curTemp >= targetTemp) {
						// Too high - reduce load
						suspend = suspend + 0.001 * (curTemp - targetTemp);
					}
					else {
						// Too low, increase load
						suspend = suspend * 0.999;
					}
					suspend = std::clamp(suspend, 0.0, 1.0);

					/*
					 * Try to avoid CPU turbo for single cores - it is power inefficient:
					 * Always use either ALL threads or NO threads at all.
					 */
					if (suspend > ratio) {
						if (open) {
							/*
							* Grab and hold sem, so nobody can start new work
							*/
							semActive.acquire();
							open = false;
						}
					}
					else {
						if (!open) {
							semActive.release();
							open = true;
						}
					}
					ratio = 0.99 * ratio + 0.01 * (open ? 0 : 1);

					if (myName == "my_local_username_instead_of_the_UPB_computers") {
						printf("[D%d, %02d:%02d] ", now_tm->tm_wday, now_tm->tm_hour, now_tm->tm_min);
						std::cout
							<< "Temperature " << curTemp
							//<< " (" << (deltaTemp >= 0 ? "+" : "") << deltaTemp << ")"
							<< " / " << targetTemp
							<< ", suspend " << suspend
							<< ", ratio " << ratio
							<< ", open " << (open ? 1 : 0)
							<< "\n";
					}
				}
			}
		}
	);
#endif

	std::vector<std::string> args(argv, argv + argc);
	for (size_t i = 1; i < args.size(); i++) {
		const auto& arg = args[i];

		if (arg == "--help") {
			printHelp();
			continue;
		}

		if (arg == "--targetTempDaytime") {
			targetTempDaytime = std::stod(args[++i]);
			continue;
		}

		if (arg == "--verbose") {
			verbose = true;
			continue;
		}

		if (arg == "--binary-output") {
			// (This was never tested)
			binaryOutput = true;
			continue;
		}

		if (arg == "--selftest") {
			Grid test("000,000,011");
			do {
				std::cout << test.toString("\n") << "\n";
				if (test.isGridMinimum()) {
					std::cout << "IS canonical\n";
				}
				else {
					std::cout << "not canonical\n";
				}
			} while (test.nextPermutation());
			continue;
		}

		if (arg == "--perftest") {
			Grid test("0000000000,0000000000,0000000000,0000000000,0000000000,0000000000");
			auto t0 = std::chrono::steady_clock::now();
			Matrix const* pResult = 0;
			doGrid(test, &pResult);
			const auto nsol = pResult->nSolutions;
			delete pResult;
			std::cout << "Found " << nsol << " solutions after " << timeFromD(t0) << "s\n";
			if (nsol != 9356) {
				std::cout << "WARNING: Number of solutions to 6x10 board should be 9356\n";
			}
			continue;
		}

		if (arg == "--grid") {
			Grid grid(args[++i]);
			doGrid(grid, 0);
			continue;
		}

		if (arg == "--out") {
			outfile = args[++i];
			if (verbose) {
				std::cout << "Writing to " << outfile << "\n";
			}
			continue;
		}

		if (arg == "--table") {
			focusUniq = true;
			maxSolutionsToPrint = 0;

			Grid grid(args[++i]);
			while (true) {
				auto const pLocalGrid = new Grid(grid);
				if (pLocalGrid->isGridMinimum()) {
					doGrid(*pLocalGrid, 0);
				}
				delete pLocalGrid;
				if (!grid.nextPermutation()) {
					break;
				}
			}
			continue;
		}

		if (arg == "--from-file") {
			std::string from = args[++i];
			std::ifstream fromfile;
			std::istream& read = (from == "-")
				? std::cin
				: (fromfile.open(from), fromfile);
			if (!read) {
				std::cout << "Could not open input file.\n";
				return 2;
			}

			std::string word;
			while (read >> word) {
#ifdef __linux__
				/*
				 * When running at UPB, pause if another user logged in
				 */
				while (true) {
					auto users = system_exec("users | tr ' ' '\n' | sort -u");
					if (users == "") {
						break;
				}
					users.pop_back(); // Remove trailing newline
					if (users == myName || users == "root") {
						break;
					}
					//std::cout << "Detected login of user <" << users << ">, sleeping (my name is <" << myName << ">)\n";
					//return 101;
					std::this_thread::sleep_for(std::chrono::minutes(1));
				}
#endif

				if (word == "test-permute") {
					size_t count;
					std::string sGrid;
					read >> count;
					read >> sGrid;

					auto t0 = std::chrono::steady_clock::now();
					Grid grid(sGrid);
					for (size_t i = 0; i < count; i++) {
						if (!grid.nextPermutation()) {
							break;
						}
					}
					std::cout << "Permutations completed after " << timeFromD(t0) << "s\n";
					continue;
				}

				if (word == "cumulative") {
					cumulative = true;
					maxSolutionsToPrint = 0;
					continue;
				}

				if (word == "uniq") {
					focusUniq = true;
					continue;
				}

				if (word == "consec") {
					size_t count;
					std::string sGrid;
					read >> count;
					read >> sGrid;

					Grid grid(sGrid);
					Stats stats;
					pStats = &stats;
					auto t0 = std::chrono::steady_clock::now();
					if (cumulative) {
						stats.board0 = grid.toString(",");
						stats.timeStart = uint64_t(std::chrono::duration_cast<std::chrono::seconds>(
							std::chrono::system_clock::now().time_since_epoch()
							).count());
					}

					/*
					 * Fill work queue with work
					 */
					spaceAvailable.release(maxThreads * 10u);
					for (size_t i = 0; i < count; i++) {
						++stats.permuted;
						auto const pLocalGrid = new Grid(grid);
						spaceAvailable.acquire();
						{
							DBG("add work " << pLocalGrid);
							std::unique_lock<std::mutex> mlock(mWork);
							work.push(pLocalGrid);
						}
						workAvailable.release();
						if (!grid.nextPermutation()) {
							if (verbose) {
								std::cout << "No more permutations\n";
							}
							break;
						}
					}
					DBG("all work added");

					/*
					 * Wait until all threads have completed work
					 */
					for (size_t i = 0; i < maxThreads * 10u; i++) {
						spaceAvailable.acquire();
					}
					DBG("all work finished");

					if (cumulative) {
						stats.timeTotalSeconds = timeFromD(t0);

						//stats.board2 = grid.toString(",");
						grid.prevPermutation();
						stats.board1 = grid.toString(",");
					}

					std::stringstream ss0;
					std::stringstream ss1;
					if (cumulative) {
						ss0 << "programVersion\t";
						ss1 << "44," << stats.programVersion << "\t";
						ss0 << "timeStart\t";
						ss1 << stats.timeStart << "\t";
						ss0 << "timeTotalSeconds\t";
						ss1 << stats.timeTotalSeconds << "\t";
						ss0 << "parallelThreads\t";
						ss1 << maxThreads << "\t";
						ss0 << "focusUniq\t";
						ss1 << focusUniq << "\t";

						//TODO speichern ob gedrosselt

						ss0 << "board0\t";
						ss1 << stats.board0 << "\t";
						ss0 << "board1\t";
						ss1 << stats.board1 << "\t";
						//ss0 << "board2\t";
						//ss1 << stats.board2 << "\t";

						ss0 << "permutated\t";
						ss1 << stats.permuted << "\t";
						ss0 << "canonical\t";
						ss1 << stats.canonical << "\t";

						ss0 << "unsolvableN\t";
						ss1 << stats.unsolvableN << "\t";
						ss0 << "unsolvableSumSteps\t";
						ss1 << stats.unsolvableSumSteps << "\t";
						ss0 << "unsolvableMaxSteps\t";
						ss1 << stats.unsolvableMaxSteps << "\t";

						ss0 << "solvableN\t";
						ss1 << stats.solvableN << "\t";
						ss0 << "solvableSumSteps\t";
						ss1 << stats.solvableSumSteps << "\t";
						ss0 << "solvableMaxSteps\t";
						ss1 << stats.solvableMaxSteps << "\t";
						ss0 << "solvableSumSolutions\t";
						ss1 << stats.solvableSumSolutions << "\t";
						ss0 << "solvableMaxSolutions\t";
						ss1 << stats.solvableMaxSolutions << "\t";

						ss0 << "uniqN\t";
						ss1 << stats.uniqN << "\t";
						ss0 << "uniqSumSteps\t";
						ss1 << stats.uniqSumSteps << "\t";
						ss0 << "uniqMaxSteps\t";
						ss1 << stats.uniqMaxSteps << "\t";

						ss0 << "end\n";
						ss1 << "end\n";
					}

					/*
					std::stringstream sscumulfile;
					sscumulfile << outdir;
					sscumulfile << (int)grid.SX() << "," << (int)grid.SY() << "__";
					sscumulfile << stats.board0;
					sscumulfile << "__";
					sscumulfile << rand() % 10000;
					sscumulfile << ".tsv";
					std::ofstream fout(sscumulfile.str());
					*/

					const auto oflags = std::ofstream::out
						| std::ofstream::ate
						| std::ofstream::app;
					std::ofstream fout(outfile, oflags);
					//fout << ss0.str();
					fout << ss1.str();

					//std::cout << ss0.str();
					//std::cout << ss1.str();

					pStats = 0;
					continue;
				}

				doGrid(word, 0);
			}
			continue;
		}

		std::cout << "Unknown argument, quitting: " << arg << "\n";
		printHelp();
		return 1;
	}

#ifdef __linux__
	/*
	 * Wait until all threads are terminated
	 */
	please_end = true;
	for (size_t i = 0; i < maxThreads; i++) {
		workAvailable.release();
	}
	threadTemp.join();
	for (size_t i = 0; i < maxThreads; i++) {
		threads[i].join();
	}
#endif

	std::cout.flush();

	return 0;
}
