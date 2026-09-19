#pragma once

#include <mutex>
#include <condition_variable>



class Semaphore {
private:
	std::mutex _mutex;
	std::condition_variable _cv;
	size_t _count;

public:
	Semaphore(size_t initialCount = 0)
		: _count(initialCount)
	{}

	void release() {
		{
			std::lock_guard<decltype(_mutex)> lock(_mutex);
			++_count;
		}
		_cv.notify_one();
	}

	void release(size_t howMany) {
		{
			std::lock_guard<decltype(_mutex)> lock(_mutex);
			_count += howMany;
		}
		_cv.notify_all();
	}

	void acquire() {
		std::unique_lock<decltype(_mutex)> lock(_mutex);
		while (!_count) {
			// spurious wakeup
			_cv.wait(lock);
		}
		--_count;
	}

	bool try_acquire() {
		std::lock_guard<decltype(_mutex)> lock(_mutex);
		if (_count) {
			--_count;
			return true;
		}
		return false;
	}
};
