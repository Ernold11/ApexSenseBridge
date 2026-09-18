#pragma once

#include "dualsense/VirtualDualSense.h"

#include <memory>

namespace asb::dualsense {

// Publishes the controller through the kernel's uhid interface. Needs no
// privileges and no kernel modules beyond uhid itself, but can only ever be a
// HID device: it has no USB topology and no audio endpoint.
std::unique_ptr<VirtualDualSense> createUhidVirtualDualSense(
    VirtualDualSenseOptions options);

// Publishes the controller as a real USB device through libVIIPER and the
// kernel's vhci-hcd, the same library the Windows build uses. Gains a genuine
// usb_device ancestor and the four-channel audio endpoint DualSense haptics
// travel on, at the cost of needing usbip(8) and root to attach.
std::unique_ptr<VirtualDualSense> createLibViiperVirtualDualSense(
    VirtualDualSenseOptions options);

} // namespace asb::dualsense
