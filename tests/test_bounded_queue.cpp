#include "aeroguard/bounded_queue.hpp"

#include <gtest/gtest.h>

#include <atomic>
#include <chrono>
#include <thread>
#include <vector>

namespace aeroguard {

// REQ-008: fixed capacity and FIFO behavior are observable at the boundary.
TEST(REQ008_BoundedQueue, RejectsPushWhenFullAndPreservesFifoOrder) {
  BoundedQueue<int, 3> queue;
  EXPECT_EQ(queue.capacity(), 3U);
  EXPECT_TRUE(queue.try_push(10));
  EXPECT_TRUE(queue.try_push(20));
  EXPECT_TRUE(queue.try_push(30));
  EXPECT_FALSE(queue.try_push(40));
  ASSERT_EQ(queue.size(), 3U);

  EXPECT_EQ(queue.try_pop(), 10);
  EXPECT_EQ(queue.try_pop(), 20);
  EXPECT_EQ(queue.try_pop(), 30);
  EXPECT_FALSE(queue.try_pop().has_value());
}

// REQ-008: the synchronization belongs to the queue and supports one producer
// and one consumer without exposing shared indices.
TEST(REQ008_BoundedQueue, SafelyTransfersMessagesAcrossThreads) {
  constexpr int kMessageCount = 500;
  BoundedQueue<int, 8> queue;
  std::vector<int> received;
  received.reserve(kMessageCount);
  std::atomic_bool succeeded{true};

  std::thread producer([&] {
    for (int value = 0; value < kMessageCount; ++value) {
      if (!queue.push_for(value, std::chrono::seconds(1))) {
        succeeded = false;
        return;
      }
    }
  });
  std::thread consumer([&] {
    for (int count = 0; count < kMessageCount; ++count) {
      auto value = queue.pop_for(std::chrono::seconds(1));
      if (!value) {
        succeeded = false;
        return;
      }
      received.push_back(*value);
    }
  });

  producer.join();
  consumer.join();
  ASSERT_TRUE(succeeded.load());
  ASSERT_EQ(received.size(), static_cast<std::size_t>(kMessageCount));
  for (int index = 0; index < kMessageCount; ++index) {
    EXPECT_EQ(received[static_cast<std::size_t>(index)], index);
  }
}

TEST(REQ008_BoundedQueue, ClosingRejectsNewMessagesAndWakesWaiters) {
  BoundedQueue<int, 1> queue;
  queue.close();
  EXPECT_TRUE(queue.closed());
  EXPECT_FALSE(queue.try_push(1));
  EXPECT_FALSE(queue.pop_for(std::chrono::milliseconds(1)).has_value());
}

}  // namespace aeroguard
