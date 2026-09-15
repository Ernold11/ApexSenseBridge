#include "flydigi/Apex5Input.h"

#include "flydigi/Apex5Protocol.h"
#include "platform/XInputMapping.h"

#include <cstdint>

namespace asb::flydigi {
namespace {

std::int16_t signedLittleEndian(std::span<const std::uint8_t> report,
                                std::size_t offset) noexcept {
    const auto raw = static_cast<std::uint16_t>(
        report[offset] |
        static_cast<std::uint16_t>(report[offset + 1]) << 8U);
    return static_cast<std::int16_t>(raw);
}

} // namespace

std::optional<dualsense::DualSenseInputState>
decodeApex5InputReport(std::span<const std::uint8_t> report) noexcept {
    if (report.size() < 18 || report[0] != kReportIdIn ||
        report[1] != kMagic0 || report[2] != kMagic1 ||
        report[3] != kCmdOperatorData) {
        return std::nullopt;
    }

    platform::XInputSnapshot snapshot{};
    snapshot.leftX = signedLittleEndian(report, 4);
    snapshot.leftY = signedLittleEndian(report, 6);
    snapshot.rightX = signedLittleEndian(report, 8);
    snapshot.rightY = signedLittleEndian(report, 10);
    snapshot.leftTrigger = report[16];
    snapshot.rightTrigger = report[17];

    const auto primary = report[12];
    const auto secondary = report[13];
    if (primary & 0x01) snapshot.buttons |= platform::xinputButton::kDpadUp;
    if (primary & 0x02) snapshot.buttons |= platform::xinputButton::kDpadRight;
    if (primary & 0x04) snapshot.buttons |= platform::xinputButton::kDpadDown;
    if (primary & 0x08) snapshot.buttons |= platform::xinputButton::kDpadLeft;
    if (primary & 0x10) snapshot.buttons |= platform::xinputButton::kA;
    if (primary & 0x20) snapshot.buttons |= platform::xinputButton::kB;
    if (primary & 0x40) snapshot.buttons |= platform::xinputButton::kBack;
    if (primary & 0x80) snapshot.buttons |= platform::xinputButton::kX;
    if (secondary & 0x01) snapshot.buttons |= platform::xinputButton::kY;
    if (secondary & 0x02) snapshot.buttons |= platform::xinputButton::kStart;
    if (secondary & 0x04) snapshot.buttons |= platform::xinputButton::kLeftShoulder;
    if (secondary & 0x08) snapshot.buttons |= platform::xinputButton::kRightShoulder;
    if (secondary & 0x40) snapshot.buttons |= platform::xinputButton::kLeftThumb;
    if (secondary & 0x80) snapshot.buttons |= platform::xinputButton::kRightThumb;

    auto state = platform::mapXInputState(snapshot);
    if (report[15] & 0x08) state.buttons |= dualsense::button::kPs;
    return state;
}

} // namespace asb::flydigi
