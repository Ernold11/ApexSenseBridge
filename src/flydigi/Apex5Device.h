#pragma once

#include "core/DeviceInfo.h"
#include "core/TriggerEffect.h"
#include "flydigi/Apex5Identity.h"
#include "platform/HidTransport.h"

#include <chrono>
#include <memory>
#include <optional>
#include <span>
#include <string>
#include <vector>

namespace asb::flydigi {

struct TransportDeleter {
    void operator()(platform::HidTransport* transport) const noexcept;
};
using TransportPtr = std::unique_ptr<platform::HidTransport, TransportDeleter>;

class Apex5Device {
public:
    // Historical API name retained for source compatibility. This transport
    // now supports both Apex 5 (Flydigi V2) and Apex 4 (Flydigi V1/DInput).
    Apex5Device() = default;
    explicit Apex5Device(TransportPtr transport);

    Apex5Device(const Apex5Device&) = delete;
    Apex5Device& operator=(const Apex5Device&) = delete;
    Apex5Device(Apex5Device&&) noexcept = default;
    Apex5Device& operator=(Apex5Device&&) noexcept = default;

    [[nodiscard]] static std::vector<HidDeviceInfo> findCandidates(std::string& error);
    [[nodiscard]] static std::optional<Apex5Device> open(const HidDeviceInfo& info, std::string& error);

    [[nodiscard]] bool isOpen() const noexcept;
    [[nodiscard]] const HidDeviceInfo& info() const;
    [[nodiscard]] const std::optional<Apex5Identity>& identity() const noexcept;

    bool verifyIdentity(std::string& error);

    bool setTrigger(const TriggerEffect& effect, std::string& error);
    bool setTriggerRaw(const ForceTriggerCommand& command, std::string& error);
    bool clearTrigger(TriggerSide side, std::string& error);
    bool clearAll(std::string& error);
    bool setRumble(std::uint8_t lowFrequencyMotor,
                   std::uint8_t highFrequencyMotor,
                   std::string& error);
    bool stopRumble(std::string& error);
    bool readProfileStatus(ProfileStatus& status, std::string& error);
    bool applyProfile(std::uint8_t slot, std::string& error);
    bool readInputTransportStatus(InputTransportStatus& status,
                                  std::string& error);
    bool setInputTransport(bool controllerData, bool rawData,
                           std::string& error);

private:
    // Effect and rumble writes go through here rather than straight to the
    // transport. Two vendor commands sent close together lose the second one:
    // measured on an Apex 4, a release followed 4.3 ms later by an effect leaves
    // no resistance at all, while the same pair 10 ms apart works every time.
    //
    // That is exactly the shape a game produces. Cyberpunk 2077 sends a release
    // and then the effect 4.3 ms later on every aim after the first, and applies
    // both triggers from one report - so the left trigger, written second, was
    // the one that always vanished.
    [[nodiscard]] bool writeSpacedOutputReport(std::span<const std::uint8_t> report,
                                               std::string& error);

    [[nodiscard]] bool mayWriteEffects(std::string& error) const;
    [[nodiscard]] bool mayControlProfiles(std::string& error) const;
    [[nodiscard]] bool usesApex4Protocol() const noexcept;

    TransportPtr transport_{};
    std::optional<Apex5Identity> identity_{};
    std::chrono::steady_clock::time_point lastVendorWriteAt_{};
};

} // namespace asb::flydigi
