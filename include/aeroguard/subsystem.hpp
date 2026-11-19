#pragma once

#include "aeroguard/hal.hpp"
#include "aeroguard/types.hpp"

#include <array>
#include <cstdint>
#include <memory>
#include <optional>

namespace aeroguard {

class ISubsystem {
 public:
  virtual ~ISubsystem() = default;
  [[nodiscard]] virtual SubsystemId id() const noexcept = 0;
  [[nodiscard]] virtual std::optional<TelemetryMessage> poll(
      IHardwareAbstraction& hardware, SimTime now) = 0;
};

class PowerSupply final : public ISubsystem {
 public:
  [[nodiscard]] SubsystemId id() const noexcept override {
    return SubsystemId::Power;
  }
  [[nodiscard]] std::optional<TelemetryMessage> poll(
      IHardwareAbstraction& hardware, SimTime now) override;

 private:
  std::uint64_t sequence_{};
};

class CoolingSystem final : public ISubsystem {
 public:
  [[nodiscard]] SubsystemId id() const noexcept override {
    return SubsystemId::Cooling;
  }
  [[nodiscard]] std::optional<TelemetryMessage> poll(
      IHardwareAbstraction& hardware, SimTime now) override;

 private:
  std::uint64_t sequence_{};
};

class TemperatureSensors final : public ISubsystem {
 public:
  [[nodiscard]] SubsystemId id() const noexcept override {
    return SubsystemId::Temperature;
  }
  [[nodiscard]] std::optional<TelemetryMessage> poll(
      IHardwareAbstraction& hardware, SimTime now) override;

 private:
  std::uint64_t sequence_{};
};

class CommunicationModule final : public ISubsystem {
 public:
  [[nodiscard]] SubsystemId id() const noexcept override {
    return SubsystemId::Communication;
  }
  [[nodiscard]] std::optional<TelemetryMessage> poll(
      IHardwareAbstraction& hardware, SimTime now) override;

 private:
  std::uint64_t sequence_{};
};

class OnboardComputer final : public ISubsystem {
 public:
  [[nodiscard]] SubsystemId id() const noexcept override {
    return SubsystemId::Computer;
  }
  [[nodiscard]] std::optional<TelemetryMessage> poll(
      IHardwareAbstraction& hardware, SimTime now) override;

 private:
  std::uint64_t sequence_{};
};

class EquipmentStatusMonitor final : public ISubsystem {
 public:
  [[nodiscard]] SubsystemId id() const noexcept override {
    return SubsystemId::Equipment;
  }
  [[nodiscard]] std::optional<TelemetryMessage> poll(
      IHardwareAbstraction& hardware, SimTime now) override;

 private:
  std::uint64_t sequence_{};
};

using SubsystemCollection =
    std::array<std::unique_ptr<ISubsystem>, kSubsystemCount>;
[[nodiscard]] SubsystemCollection make_subsystems();

}  // namespace aeroguard
