using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using OpenHardwareMonitor.Hardware.Cpu;

namespace OpenHardwareMonitor.Hardware.Motherboard.Lpc;

internal class LpcIO
{
    private readonly StringBuilder _report = new();
    private readonly List<ISuperIO> _superIOs = new();

    public LpcIO(Motherboard motherboard)
    {
        if (!Ring0.IsOpen || !Mutexes.WaitIsaBus(100))
            return;

        Detect(motherboard);

        Mutexes.ReleaseIsaBus();

        if (Ipmi.IsBmcPresent())
            _superIOs.Add(new Ipmi(motherboard.Manufacturer));
    }

    public ISuperIO[] SuperIO => _superIOs.ToArray();

    private void ReportUnknownChip(LpcPort port, string type, int chip)
    {
        _report.Append("Chip ID: Unknown ");
        _report.Append(type);
        _report.Append(" with ID 0x");
        _report.Append(chip.ToString("X", CultureInfo.InvariantCulture));
        _report.Append(" at 0x");
        _report.Append(port.RegisterPort.ToString("X", CultureInfo.InvariantCulture));
        _report.Append("/0x");
        _report.AppendLine(port.ValuePort.ToString("X", CultureInfo.InvariantCulture));
        _report.AppendLine();
    }

    private bool DetectSmsc(LpcPort port)
    {
        port.SmscEnter();

        ushort chipId = port.ReadWord(CHIP_ID_REGISTER);

        if (chipId is not 0 and not 0xffff)
        {
            port.SmscExit();
            ReportUnknownChip(port, "SMSC", chipId);
        }

        return false;
    }

    private void Detect(Motherboard motherboard)
    {
        for (int i = 0; i < REGISTER_PORTS.Length; i++)
        {
            var port = new LpcPort(REGISTER_PORTS[i], VALUE_PORTS[i]);

            if (DetectWinbondFintek(port, motherboard)) continue;

            if (DetectIT87(port, motherboard)) continue;

            if (DetectSmsc(port)) continue;
        }
    }

    public string GetReport()
    {
        if (_report.Length > 0)
        {
            return "LpcIO" + Environment.NewLine + Environment.NewLine + _report;
        }

        return null;
    }

    private bool DetectWinbondFintek(LpcPort port, Motherboard motherboard)
    {
        port.WinbondNuvotonFintekEnter();

        byte logicalDeviceNumber = 0;
        byte id = port.ReadByte(CHIP_ID_REGISTER);
        byte revision = port.ReadByte(CHIP_REVISION_REGISTER);
        Chip chip = Chip.Unknown;

        switch (id)
        {
            case 0x05:
                switch (revision)
                {
                    case 0x07:
                        chip = Chip.F71858;
                        logicalDeviceNumber = F71858_HARDWARE_MONITOR_LDN;
                        break;
                    case 0x41:
                        chip = Chip.F71882;
                        logicalDeviceNumber = FINTEK_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0x06:
                switch (revision)
                {
                    case 0x01:
                        chip = Chip.F71862;
                        logicalDeviceNumber = FINTEK_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0x07:
                switch (revision)
                {
                    case 0x23:
                        chip = Chip.F71889F;
                        logicalDeviceNumber = FINTEK_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0x08:
                switch (revision)
                {
                    case 0x14:
                        chip = Chip.F71869;
                        logicalDeviceNumber = FINTEK_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0x09:
                switch (revision)
                {
                    case 0x01:
                        chip = Chip.F71808E;
                        logicalDeviceNumber = FINTEK_HARDWARE_MONITOR_LDN;
                        break;
                    case 0x09:
                        chip = Chip.F71889ED;
                        logicalDeviceNumber = FINTEK_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0x10:
                switch (revision)
                {
                    case 0x05:
                        chip = Chip.F71889AD;
                        logicalDeviceNumber = FINTEK_HARDWARE_MONITOR_LDN;
                        break;
                    case 0x07:
                        chip = Chip.F71869A;
                        logicalDeviceNumber = FINTEK_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0x11:
                switch (revision)
                {
                    case 0x06:
                        chip = Chip.F71878AD;
                        logicalDeviceNumber = FINTEK_HARDWARE_MONITOR_LDN;
                        break;
                    case 0x18:
                        chip = Chip.F71811;
                        logicalDeviceNumber = FINTEK_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0x52:
                switch (revision)
                {
                    case 0x17:
                    case 0x3A:
                    case 0x41:
                        chip = Chip.W83627HF;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0x82:
                switch (revision & 0xF0)
                {
                    case 0x80:
                        chip = Chip.W83627THF;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0x85:
                switch (revision)
                {
                    case 0x41:
                        chip = Chip.W83687THF;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0x88:
                switch (revision & 0xF0)
                {
                    case 0x50:
                    case 0x60:
                        chip = Chip.W83627EHF;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0xA0:
                switch (revision & 0xF0)
                {
                    case 0x20:
                        chip = Chip.W83627DHG;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0xA5:
                switch (revision & 0xF0)
                {
                    case 0x10:
                        chip = Chip.W83667HG;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0xB0:
                switch (revision & 0xF0)
                {
                    case 0x70:
                        chip = Chip.W83627DHGP;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0xB3:
                switch (revision & 0xF0)
                {
                    case 0x50:
                        chip = Chip.W83667HGB;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0xB4:
                switch (revision & 0xF0)
                {
                    case 0x70:
                        chip = Chip.NCT6771F;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0xC3:
                switch (revision & 0xF0)
                {
                    case 0x30:
                        chip = Chip.NCT6776F;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0xC4:
                switch (revision & 0xF0)
                {
                    case 0x50:
                        chip = Chip.NCT610XD;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0xC5:
                switch (revision & 0xF0)
                {
                    case 0x60:
                        chip = Chip.NCT6779D;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0xC7:
                switch (revision)
                {
                    case 0x32:
                        chip = Chip.NCT6683D;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0xC8:
                switch (revision)
                {
                    case 0x03:
                        chip = Chip.NCT6791D;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0xC9:
                switch (revision)
                {
                    case 0x11:
                        chip = Chip.NCT6792D;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                    case 0x13:
                        chip = Chip.NCT6792DA;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0xD1:
                switch (revision)
                {
                    case 0x21:
                        chip = Chip.NCT6793D;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0xD3:
                switch (revision)
                {
                    case 0x52:
                        chip = Chip.NCT6795D;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0xD4:
                switch (revision)
                {
                    case 0x23:
                        chip = Chip.NCT6796D;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                    case 0x2A:
                        chip = Chip.NCT6796DR;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                    case 0x51:
                        chip = Chip.NCT6797D;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                    case 0x2B:
                        chip = Chip.NCT6798D;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                    case 0x40:
                    case 0x41:
                        chip = Chip.NCT6686D;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0xD5:
                switch (revision)
                {
                    case 0x92:
                        switch (motherboard.Model)
                        {
                            case Model.B840P_PRO_WIFI:
                            case Model.B850_GAMING_PLUS_WIFI:
                            case Model.B850P_PRO_WIFI:
                            case Model.B850M_MORTAR_WIFI:
                            case Model.B850_TOMAHAWK_MAX_WIFI:
                            case Model.B850_EDGE_TI_WIFI:
                            case Model.X870_GAMING_PLUS_WIFI:
                            case Model.X870_TOMAHAWK_WIFI:
                            case Model.X870P_PRO_WIFI:
                            case Model.X870E_TOMAHAWK_WIFI:
                            case Model.X870E_CARBON_WIFI:
                            case Model.X870E_EDGE_TI_WIFI:
                            case Model.X870E_GODLIKE:
                            case Model.Z890_ACE:
                            case Model.Z890_CARBON_WIFI:
                            case Model.Z890_TOMAHAWK_WIFI:
                            case Model.Z890_EDGE_TI_WIFI:
                            case Model.Z890P_PRO_WIFI:
                            case Model.Z890A_PRO_WIFI:
                                chip = Chip.NCT6687DR; // MSI AM5/LGA1851 Compatibility
                                break;
                            default:
                                chip = Chip.NCT6687D;
                                break;
                        }

                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
            case 0xD8:
                switch (revision)
                {
                    case 0x02:
                        chip = Chip.NCT6799D;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                    case 0x06:
                        chip = Chip.NCT6701D;
                        logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                        break;
                }

                break;
        }

        if (chip == Chip.Unknown)
        {
            if (id is not 0 and not 0xff)
            {
                port.WinbondNuvotonFintekExit();
                ReportUnknownChip(port, "Winbond / Nuvoton / Fintek", (id << 8) | revision);
            }
        }
        else
        {
            port.Select(logicalDeviceNumber);
            ushort address = port.ReadWord(BASE_ADDRESS_REGISTER);
            Thread.Sleep(1);
            ushort verify = port.ReadWord(BASE_ADDRESS_REGISTER);

            ushort vendorId = port.ReadWord(FINTEK_VENDOR_ID_REGISTER);

            // disable the hardware monitor i/o space lock on NCT679XD chips
            if (address == verify &&
                chip is Chip.NCT6791D or Chip.NCT6792D or Chip.NCT6792DA or Chip.NCT6793D or Chip.NCT6795D or Chip.NCT6796D or Chip.NCT6796DR or Chip.NCT6798D or Chip.NCT6797D or Chip.NCT6799D or Chip.NCT6701D)
            {
                port.NuvotonDisableIOSpaceLock();
            }

            port.WinbondNuvotonFintekExit();

            if (address != verify)
            {
                _report.Append("Chip ID: 0x");
                _report.AppendLine(chip.ToString("X"));
                _report.Append("Chip revision: 0x");
                _report.AppendLine(revision.ToString("X", CultureInfo.InvariantCulture));
                _report.AppendLine("Error: Address verification failed");
                _report.AppendLine();

                return false;
            }

            // some Fintek chips have address register offset 0x05 added already
            if ((address & 0x07) == 0x05)
                address &= 0xFFF8;

            if (address < 0x100 || (address & 0xF007) != 0)
            {
                _report.Append("Chip ID: 0x");
                _report.AppendLine(chip.ToString("X"));
                _report.Append("Chip revision: 0x");
                _report.AppendLine(revision.ToString("X", CultureInfo.InvariantCulture));
                _report.Append("Error: Invalid address 0x");
                _report.AppendLine(address.ToString("X", CultureInfo.InvariantCulture));
                _report.AppendLine();

                return false;
            }

            switch (chip)
            {
                case Chip.W83627DHG:
                case Chip.W83627DHGP:
                case Chip.W83627EHF:
                case Chip.W83627HF:
                case Chip.W83627THF:
                case Chip.W83667HG:
                case Chip.W83667HGB:
                case Chip.W83687THF:
                    _superIOs.Add(new W836XX(chip, revision, address));
                    break;

                case Chip.NCT610XD:
                case Chip.NCT6771F:
                case Chip.NCT6776F:
                case Chip.NCT6779D:
                case Chip.NCT6791D:
                case Chip.NCT6792D:
                case Chip.NCT6792DA:
                case Chip.NCT6793D:
                case Chip.NCT6795D:
                case Chip.NCT6796D:
                case Chip.NCT6796DR:
                case Chip.NCT6797D:
                case Chip.NCT6798D:
                case Chip.NCT6799D:
                case Chip.NCT6686D:
                case Chip.NCT6687D:
                case Chip.NCT6687DR:
                case Chip.NCT6683D:
                case Chip.NCT6701D:
                    _superIOs.Add(new Nct677X(chip, revision, address, port));
                    break;

                case Chip.F71858:
                case Chip.F71862:
                case Chip.F71869:
                case Chip.F71878AD:
                case Chip.F71869A:
                case Chip.F71882:
                case Chip.F71889AD:
                case Chip.F71889ED:
                case Chip.F71889F:
                case Chip.F71808E:
                    if (vendorId != FINTEK_VENDOR_ID)
                    {
                        _report.Append("Chip ID: 0x");
                        _report.AppendLine(chip.ToString("X"));
                        _report.Append("Chip revision: 0x");
                        _report.AppendLine(revision.ToString("X", CultureInfo.InvariantCulture));
                        _report.Append("Error: Invalid vendor ID 0x");
                        _report.AppendLine(vendorId.ToString("X", CultureInfo.InvariantCulture));
                        _report.AppendLine();

                        return false;
                    }

                    _superIOs.Add(new F718XX(chip, address));
                    break;
            }

            return true;
        }

        return false;
    }

    private bool DetectIT87(LpcPort port, Motherboard motherboard)
    {
        // IT87XX can enter only on port 0x2E
        // IT8792 using 0x4E
        if (port.RegisterPort is not 0x2E and not 0x4E)
            return false;

        // Read the chip ID before entering.
        // If already entered (not 0xFFFF) and the register port is 0x4E, it is most likely bugged and should be left alone.
        // Entering IT8792 in this state will result in IT8792 reporting with chip ID of 0x8883.
        if (port.RegisterPort != 0x4E || !port.TryReadWord(CHIP_ID_REGISTER, out ushort chipId))
        {
            port.IT87Enter();
            chipId = port.ReadWord(CHIP_ID_REGISTER);
        }

        Chip chip = chipId switch
        {
            0x8613 => Chip.IT8613E,
            0x8620 => Chip.IT8620E,
            0x8625 => Chip.IT8625E,
            0x8628 => Chip.IT8628E,
            0x8631 => Chip.IT8631E,
            0x8665 => Chip.IT8665E,
            0x8655 => Chip.IT8655E,
            0x8686 => Chip.IT8686E,
            0x8688 => Chip.IT8688E,
            0x8689 => Chip.IT8689E,
            0x8696 => Chip.IT8696E,
            0x8705 => Chip.IT8705F,
            0x8712 => Chip.IT8712F,
            0x8716 => Chip.IT8716F,
            0x8718 => Chip.IT8718F,
            0x8720 => Chip.IT8720F,
            0x8721 => Chip.IT8721F,
            0x8726 => Chip.IT8726F,
            0x8728 => Chip.IT8728F,
            0x8771 => Chip.IT8771E,
            0x8772 => Chip.IT8772E,
            0x8790 => Chip.IT8790E,
            0x8733 => Chip.IT8792E,
            0x8695 => Chip.IT87952E,
            _ => Chip.Unknown
        };

        if (chip == Chip.Unknown)
        {
            if (chipId is not 0 and not 0xffff)
            {
                port.IT87Exit();

                ReportUnknownChip(port, "ITE", chipId);
            }
        }
        else
        {
            port.Select(IT87_ENVIRONMENT_CONTROLLER_LDN);

            ushort address = port.ReadWord(BASE_ADDRESS_REGISTER);
            Thread.Sleep(1);
            ushort verify = port.ReadWord(BASE_ADDRESS_REGISTER);

            byte version = (byte)(port.ReadByte(IT87_CHIP_VERSION_REGISTER) & 0x0F);

            ushort gpioAddress;
            ushort gpioVerify;

            if (chip == Chip.IT8705F)
            {
                port.Select(IT8705_GPIO_LDN);
                gpioAddress = port.ReadWord(BASE_ADDRESS_REGISTER);
                Thread.Sleep(1);
                gpioVerify = port.ReadWord(BASE_ADDRESS_REGISTER);
            }
            else
            {
                port.Select(IT87XX_GPIO_LDN);
                gpioAddress = port.ReadWord(BASE_ADDRESS_REGISTER + 2);
                Thread.Sleep(1);
                gpioVerify = port.ReadWord(BASE_ADDRESS_REGISTER + 2);
            }

            IGigabyteController gigabyteController = FindGigabyteEC(port, chip, motherboard);

            port.IT87Exit();

            if (address != verify || address < 0x100 || (address & 0xF007) != 0)
            {
                _report.Append("Chip ID: 0x");
                _report.AppendLine(chip.ToString("X"));
                _report.Append("Error: Invalid address 0x");
                _report.AppendLine(address.ToString("X", CultureInfo.InvariantCulture));
                _report.AppendLine();

                return false;
            }

            if (gpioAddress != gpioVerify || gpioAddress < 0x100 || (gpioAddress & 0xF007) != 0)
            {
                _report.Append("Chip ID: 0x");
                _report.AppendLine(chip.ToString("X"));
                _report.Append("Error: Invalid GPIO address 0x");
                _report.AppendLine(gpioAddress.ToString("X", CultureInfo.InvariantCulture));
                _report.AppendLine();

                return false;
            }

            _superIOs.Add(new IT87XX(chip, address, gpioAddress, version, motherboard, gigabyteController));
            return true;
        }

        return false;
    }

    private IGigabyteController FindGigabyteEC(LpcPort port, Chip chip, Motherboard motherboard)
    {
        // The controller only affects the 2nd ITE chip if present, and only a few
        // models are known to use this controller.
        // IT8795E likely to need this too, but may use different registers.
        if (motherboard.Manufacturer != Manufacturer.Gigabyte || port.RegisterPort != 0x4E || chip is not (Chip.IT8790E or Chip.IT8792E or Chip.IT87952E))
            return null;

        Vendor vendor = DetectVendor();

        IGigabyteController gigabyteController = FindGigabyteECUsingSmfi(port, chip, vendor);
        if (gigabyteController != null)
            return gigabyteController;

        // ECIO is only available on AMD motherboards with IT8791E/IT8792E/IT8795E.
        if (chip == Chip.IT8792E && vendor == Vendor.AMD)
        {
            gigabyteController = EcioPortGigabyteController.TryCreate();
            if (gigabyteController != null)
                return gigabyteController;
        }

        return null;

        Vendor DetectVendor()
        {
            string manufacturer = motherboard.SMBios.Processors[0].ManufacturerName;
            if (manufacturer.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) != -1)
                return Vendor.Intel;

            if (manufacturer.IndexOf("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase) != -1 || manufacturer.StartsWith("AMD", StringComparison.OrdinalIgnoreCase))
                return Vendor.AMD;

            return Vendor.Unknown;
        }
    }

    private IGigabyteController FindGigabyteECUsingSmfi(LpcPort port, Chip chip, Vendor vendor)
    {
        port.Select(IT87XX_SMFI_LDN);

        // Check if the SMFI logical device is enabled
        byte enabled = port.ReadByte(IT87_LD_ACTIVE_REGISTER);
        Thread.Sleep(1);
        byte enabledVerify = port.ReadByte(IT87_LD_ACTIVE_REGISTER);

        // The EC has no SMFI or it's RAM access is not enabled, assume the controller is not present
        if (enabled != enabledVerify || enabled == 0)
            return null;

        // Read the host RAM address that maps to the Embedded Controller's RAM (two registers).
        uint addressHi = 0;
        uint addressHiVerify = 0;
        uint address = port.ReadWord(IT87_SMFI_HLPC_RAM_BASE_ADDRESS_REGISTER);
        if (chip == Chip.IT87952E)
            addressHi = port.ReadByte(IT87_SMFI_HLPC_RAM_BASE_ADDRESS_REGISTER_HIGH);

        Thread.Sleep(1);
        uint addressVerify = port.ReadWord(IT87_SMFI_HLPC_RAM_BASE_ADDRESS_REGISTER);
        if (chip == Chip.IT87952E)
            addressHiVerify = port.ReadByte(IT87_SMFI_HLPC_RAM_BASE_ADDRESS_REGISTER_HIGH);

        if ((address != addressVerify) || (addressHi != addressHiVerify))
            return null;

        // Address is xryy, Host Address is FFyyx000
        // For IT87952E, Address is rzxryy, Host Address is (0xFC000000 | 0x0zyyx000)
        uint hostAddress;
        if (chip == Chip.IT87952E)
            hostAddress = 0xFC000000;
        else
            hostAddress = 0xFF000000;

        hostAddress |= (address & 0xF000) | ((address & 0xFF) << 16) | ((addressHi & 0xF) << 24);

        return new IsaBridgeGigabyteController(hostAddress, vendor);
    }

    // ReSharper disable InconsistentNaming
    private const byte BASE_ADDRESS_REGISTER = 0x60;
    private const byte CHIP_ID_REGISTER = 0x20;
    private const byte CHIP_REVISION_REGISTER = 0x21;

    private const byte F71858_HARDWARE_MONITOR_LDN = 0x02;
    private const byte FINTEK_HARDWARE_MONITOR_LDN = 0x04;
    private const byte IT87_ENVIRONMENT_CONTROLLER_LDN = 0x04;
    private const byte IT8705_GPIO_LDN = 0x05;
    private const byte IT87XX_GPIO_LDN = 0x07;

    // Shared Memory/Flash Interface
    private const byte IT87XX_SMFI_LDN = 0x0F;
    private const byte WINBOND_NUVOTON_HARDWARE_MONITOR_LDN = 0x0B;

    private const ushort FINTEK_VENDOR_ID = 0x1934;

    private const byte FINTEK_VENDOR_ID_REGISTER = 0x23;
    private const byte IT87_CHIP_VERSION_REGISTER = 0x22;
    private const byte IT87_SMFI_HLPC_RAM_BASE_ADDRESS_REGISTER = 0xF5;
    private const byte IT87_SMFI_HLPC_RAM_BASE_ADDRESS_REGISTER_HIGH = 0xFC;
    private const byte IT87_LD_ACTIVE_REGISTER = 0x30;

    private readonly ushort[] REGISTER_PORTS = { 0x2E, 0x4E };

    private readonly ushort[] VALUE_PORTS = { 0x2F, 0x4F };
    // ReSharper restore InconsistentNaming
}
