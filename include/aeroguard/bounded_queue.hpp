#pragma once

#include <array>
#include <chrono>
#include <condition_variable>
#include <cstddef>
#include <mutex>
#include <optional>
#include <utility>

namespace aeroguard {

// Fixed storage makes the maximum queue memory explicit. Synchronization is
// internal, so producers and consumers never share queue indices directly.
template <typename T, std::size_t Capacity>
class BoundedQueue final {
  static_assert(Capacity > 0, "A bounded queue needs non-zero capacity");

 public:
  BoundedQueue() = default;
  BoundedQueue(const BoundedQueue&) = delete;
  BoundedQueue& operator=(const BoundedQueue&) = delete;

  [[nodiscard]] bool try_push(T value) {
    std::lock_guard lock(mutex_);
    if (closed_ || size_ == Capacity) {
      return false;
    }
    slots_[tail_].emplace(std::move(value));
    tail_ = (tail_ + 1U) % Capacity;
    ++size_;
    not_empty_.notify_one();
    return true;
  }

  template <typename Rep, typename Period>
  [[nodiscard]] bool push_for(T value,
                              std::chrono::duration<Rep, Period> timeout) {
    std::unique_lock lock(mutex_);
    if (!not_full_.wait_for(lock, timeout,
                            [this] { return closed_ || size_ < Capacity; })) {
      return false;
    }
    if (closed_) {
      return false;
    }
    slots_[tail_].emplace(std::move(value));
    tail_ = (tail_ + 1U) % Capacity;
    ++size_;
    lock.unlock();
    not_empty_.notify_one();
    return true;
  }

  [[nodiscard]] std::optional<T> try_pop() {
    std::lock_guard lock(mutex_);
    if (size_ == 0U) {
      return std::nullopt;
    }
    auto value = std::move(slots_[head_]);
    slots_[head_].reset();
    head_ = (head_ + 1U) % Capacity;
    --size_;
    not_full_.notify_one();
    return value;
  }

  template <typename Rep, typename Period>
  [[nodiscard]] std::optional<T> pop_for(
      std::chrono::duration<Rep, Period> timeout) {
    std::unique_lock lock(mutex_);
    if (!not_empty_.wait_for(lock, timeout,
                             [this] { return closed_ || size_ > 0U; })) {
      return std::nullopt;
    }
    if (size_ == 0U) {
      return std::nullopt;
    }
    auto value = std::move(slots_[head_]);
    slots_[head_].reset();
    head_ = (head_ + 1U) % Capacity;
    --size_;
    lock.unlock();
    not_full_.notify_one();
    return value;
  }

  void close() noexcept {
    {
      std::lock_guard lock(mutex_);
      closed_ = true;
    }
    not_empty_.notify_all();
    not_full_.notify_all();
  }

  [[nodiscard]] std::size_t size() const noexcept {
    std::lock_guard lock(mutex_);
    return size_;
  }
  [[nodiscard]] static constexpr std::size_t capacity() noexcept {
    return Capacity;
  }
  [[nodiscard]] bool closed() const noexcept {
    std::lock_guard lock(mutex_);
    return closed_;
  }

 private:
  mutable std::mutex mutex_;
  std::condition_variable not_empty_;
  std::condition_variable not_full_;
  std::array<std::optional<T>, Capacity> slots_{};
  std::size_t head_{};
  std::size_t tail_{};
  std::size_t size_{};
  bool closed_{};
};

}  // namespace aeroguard
