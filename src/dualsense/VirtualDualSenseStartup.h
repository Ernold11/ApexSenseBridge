#pragma once

#include "dualsense/DualSenseFirmware.h"
#include "dualsense/DualSenseInput.h"
#include "dualsense/VirtualDualSense.h"

#include <chrono>
#include <cstddef>
#include <functional>
#include <optional>
#include <string>

namespace asb::dualsense {

enum class VirtualDualSenseStartupFailure {
    None,
    BackendOpen,
    InitialInput,
    ReadinessVerification,
    DeviceRemoval,
};

struct VirtualDualSenseStartupResult {
    std::optional<DualSenseFirmwareInfo> firmware;
    std::size_t attempts = 0;
    VirtualDualSenseStartupFailure failure = VirtualDualSenseStartupFailure::None;
    std::chrono::steady_clock::time_point inputReadyAt{};
    std::chrono::steady_clock::time_point verifiedAt{};
};

using VirtualDualSenseFirmwareProbe =
    std::function<std::optional<DualSenseFirmwareInfo>(std::string& error)>;
using VirtualDualSenseRemovalWait = std::function<bool(std::string& error)>;

// Opens and seeds a virtual DualSense, then requires the independently
// enumerated Windows HID firmware interface to become readable. A readiness
// failure is retried only after the first device has been fully closed and its
// HID interface has disappeared. Every failure leaves the backend closed.
bool startVerifiedVirtualDualSense(
    VirtualDualSense& backend,
    const DualSenseInputState& initialInput,
    VirtualDualSense::FeedbackHandler feedbackHandler,
    const VirtualDualSenseFirmwareProbe& probeFirmware,
    const VirtualDualSenseRemovalWait& waitForRemoval,
    std::size_t maximumAttempts,
    VirtualDualSenseStartupResult& result,
    std::string& error);

} // namespace asb::dualsense
