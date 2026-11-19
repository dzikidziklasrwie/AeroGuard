#include "aeroguard/subsystem.hpp"

namespace aeroguard {
namespace {

template <typename Payload>
[[nodiscard]] std::optional<TelemetryMessage> make_message(
    const SubsystemId id, const SimTime now, const std::uint64_t sequence,
    std::optional<Payload> payload) {
  if (!payload) {
    return std::nullopt;
  }
  return TelemetryMessage{id, now, sequence, true, *payload};
}

}  // namespace

std::optional<TelemetryMessage> PowerSupply::poll(
    IHardwareAbstraction& hardware, const SimTime now) {
  return make_message(id(), now, ++sequence_, hardware.read_power(now));
}

std::optional<TelemetryMessage> CoolingSystem::poll(
    IHardwareAbstraction& hardware, const SimTime now) {
  return make_message(id(), now, ++sequence_, hardware.read_cooling(now));
}

std::optional<TelemetryMessage> TemperatureSensors::poll(
    IHardwareAbstraction& hardware, const SimTime now) {
  return make_message(id(), now, ++sequence_, hardware.read_temperature(now));
}

std::optional<TelemetryMessage> CommunicationModule::poll(
    IHardwareAbstraction& hardware, const SimTime now) {
  return make_message(id(), now, ++sequence_, hardware.read_communication(now));
}

std::optional<TelemetryMessage> OnboardComputer::poll(
    IHardwareAbstraction& hardware, const SimTime now) {
  return make_message(id(), now, ++sequence_, hardware.read_computer(now));
}

std::optional<TelemetryMessage> EquipmentStatusMonitor::poll(
    IHardwareAbstraction& hardware, const SimTime now) {
  return make_message(id(), now, ++sequence_, hardware.read_equipment(now));
}

SubsystemCollection make_subsystems() {
  SubsystemCollection result{};
  result[to_index(SubsystemId::Power)] = std::make_unique<PowerSupply>();
  result[to_index(SubsystemId::Cooling)] = std::make_unique<CoolingSystem>();
  result[to_index(SubsystemId::Temperature)] =
      std::make_unique<TemperatureSensors>();
  result[to_index(SubsystemId::Communication)] =
      std::make_unique<CommunicationModule>();
  result[to_index(SubsystemId::Computer)] =
      std::make_unique<OnboardComputer>();
  result[to_index(SubsystemId::Equipment)] =
      std::make_unique<EquipmentStatusMonitor>();
  return result;
}

}  // namespace aeroguard
