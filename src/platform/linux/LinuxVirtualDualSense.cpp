#ifdef __linux__

#include "platform/linux/LinuxVirtualDualSenseBackends.h"

#include <unistd.h>

#include <filesystem>
#include <utility>

namespace asb::dualsense {
namespace {

// libVIIPER presents the pad as a real USB device, which is the only way the
// DualSense haptic audio stream reaches it - uhid creates no audio endpoint. It
// used to be reserved for an explicit --virtual-backend because attaching was
// believed to need root. It does not; it needs the USB/IP client module and
// write access to three paths, which packaging/linux/ grants to the 'input'
// group. Checking them here keeps the choice predictable: if the prerequisites
// are in place the session gets haptics, and if they are not it silently gets
// the backend that always works rather than failing outright.
[[nodiscard]] bool libViiperPrerequisitesPresent() noexcept {
    std::error_code ignored;
    if (!std::filesystem::exists("/sys/devices/platform/vhci_hcd.0", ignored)) {
        return false;
    }
    // usbip(8) attaches first and records the connection afterwards, so both
    // have to be writable or the attach is undone right after it succeeds.
    return ::access("/sys/devices/platform/vhci_hcd.0/attach", W_OK) == 0 &&
           ::access("/run/vhci_hcd", W_OK | X_OK) == 0;
}

} // namespace

std::unique_ptr<VirtualDualSense> createVirtualDualSense(VirtualDualSenseOptions options) {
    switch (options.backend) {
    case VirtualDualSenseBackend::Integrated:
        // Explicitly asked for libVIIPER, which is what the Windows build calls
        // its integrated backend too. Honour it even if the checks below would
        // have declined, so its diagnostics are what the caller sees.
        return createLibViiperVirtualDualSense(std::move(options));
    case VirtualDualSenseBackend::Sidecar:
    case VirtualDualSenseBackend::Auto:
    default:
        if (libViiperPrerequisitesPresent()) {
            return createLibViiperVirtualDualSense(std::move(options));
        }
        return createUhidVirtualDualSense(std::move(options));
    }
}

} // namespace asb::dualsense

#endif // __linux__
