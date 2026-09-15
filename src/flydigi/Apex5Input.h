#pragma once

#include "dualsense/DualSenseInput.h"

#include <cstdint>
#include <optional>
#include <span>

namespace asb::flydigi {

// Decodes the Apex 5 NewXInput operator report (command 0xEF) carried by the
// vendor HID interface. This stream remains available while the controller's
// ordinary XInput output is disabled, allowing a single visible DualSense to
// carry every standard control.
[[nodiscard]] std::optional<dualsense::DualSenseInputState>
decodeApex5InputReport(std::span<const std::uint8_t> report) noexcept;

} // namespace asb::flydigi
