using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHardwareMonitor.Hardware.Motherboard.Lpc;
using OpenHardwareMonitor.Hardware.Motherboard.Lpc.EC;

namespace OpenHardwareMonitor.Hardware.Motherboard;

/// <summary>
/// Represents the motherboard of a computer with its <see cref="LpcIO" /> and <see cref="EmbeddedController" /> as <see cref="SubHardware" />.
/// </summary>
public class Motherboard : IHardware
{
    private readonly LMSensors _lmSensors;
    private readonly LpcIO _lpcIO;
    private readonly string _name;
    private readonly ISettings _settings;
    private string _customName;

    /// <summary>
    /// Creates motherboard instance by retrieving information from <see cref="OpenHardwareMonitor.Hardware.SMBios" /> and creates a new <see cref="SubHardware" /> based on data from <see cref="LpcIO" />
    /// and <see cref="EmbeddedController" />.
    /// </summary>
    /// <param name="smBios"><see cref="OpenHardwareMonitor.Hardware.SMBios" /> table containing motherboard data.</param>
    /// <param name="settings">Additional settings passed by <see cref="IComputer" />.</param>
    public Motherboard(SMBios smBios, ISettings settings)
    {
        IReadOnlyList<ISuperIO> superIO;
        _settings = settings;
        SMBios = smBios;

        Manufacturer = smBios.Board == null ? Manufacturer.Unknown : Identification.GetManufacturer(smBios.Board.ManufacturerName);
        Model = smBios.Board == null ? Model.Unknown : Identification.GetModel(smBios.Board.ProductName);

        if (smBios.Board != null)
        {
            if (!string.IsNullOrEmpty(smBios.Board.ProductName))
            {
                if (Manufacturer == Manufacturer.Unknown)
                    _name = smBios.Board.ProductName;
                else
                    _name = Manufacturer + " " + smBios.Board.ProductName;
            }
            else
            {
                _name = Manufacturer.ToString();
            }
        }
        else
        {
            _name = nameof(Manufacturer.Unknown);
        }

        _customName = settings.GetValue(new Identifier(Identifier, "name").ToString(), _name);

        if (OperatingSystemHelper.IsUnix)
        {
            _lmSensors = new LMSensors();
            superIO = _lmSensors.SuperIO;
        }
        else
        {
            _lpcIO = new LpcIO(this);
            superIO = _lpcIO.SuperIO;
        }


        EmbeddedController embeddedController = EmbeddedController.Create(Model, settings);

        List<IHardware> subHardwareList = new List<IHardware>();

        // there may be more than 1 of the same SuperIO chip
        // group by chip
        foreach (IGrouping<Chip, ISuperIO> group in superIO.GroupBy(x => x.Chip))
        {
            // index by group
            foreach ((ISuperIO superIo, int i) in group.Select((x, i) => (x, i)))
            {
                subHardwareList.Add(new SuperIOHardware(this, superIo, Manufacturer, Model, settings, i));
            }
        }

        if (embeddedController != null)
            subHardwareList.Add(embeddedController);

        SubHardware = subHardwareList.ToArray();
    }

#pragma warning disable 67
    /// <inheritdoc />
    public event SensorEventHandler SensorAdded;

    /// <inheritdoc />
    public event SensorEventHandler SensorRemoved;
#pragma warning restore 67

    /// <inheritdoc />
    public HardwareType HardwareType => HardwareType.Motherboard;

    /// <inheritdoc />
    public Identifier Identifier => new("motherboard");

    /// <summary>
    /// Gets the <see cref="OpenHardwareMonitor.Hardware.Motherboard.Manufacturer" />.
    /// </summary>
    public Manufacturer Manufacturer { get; }

    /// <summary>
    /// Gets the <see cref="OpenHardwareMonitor.Hardware.Motherboard.Model" />.
    /// </summary>
    public Model Model { get; }

    /// <summary>
    /// Gets the name obtained from <see cref="OpenHardwareMonitor.Hardware.SMBios" />.
    /// </summary>
    public string Name
    {
        get { return _customName; }
        set
        {
            _customName = !string.IsNullOrEmpty(value) ? value : _name;

            _settings.SetValue(new Identifier(Identifier, "name").ToString(), _customName);
        }
    }

    /// <inheritdoc />
    /// <returns>Always <see langword="null" /></returns>
    public virtual IHardware Parent
    {
        get { return null; }
    }

    /// <inheritdoc />
    public virtual IDictionary<string, string> Properties => new SortedDictionary<string, string>();

    /// <inheritdoc />
    public ISensor[] Sensors
    {
        get { return Array.Empty<ISensor>(); }
    }

    /// <summary>
    /// Gets the <see cref="OpenHardwareMonitor.Hardware.SMBios" /> information.
    /// </summary>
    public SMBios SMBios { get; }

    /// <inheritdoc />
    public IHardware[] SubHardware { get; }

    /// <inheritdoc />
    public string GetReport()
    {
        StringBuilder r = new();

        r.AppendLine("Motherboard");
        r.AppendLine();
        r.Append(SMBios.GetReport());

        if (_lpcIO != null)
            r.Append(_lpcIO.GetReport());

        return r.ToString();
    }

    /// <summary>
    /// Motherboard itself cannot be updated. Update <see cref="SubHardware" /> instead.
    /// </summary>
    public void Update()
    { }

    /// <inheritdoc />
    public void Accept(IVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        visitor.VisitHardware(this);
    }

    /// <inheritdoc />
    public void Traverse(IVisitor visitor)
    {
        foreach (IHardware hardware in SubHardware)
            hardware.Accept(visitor);
    }

    /// <summary>
    /// Closes <see cref="SubHardware" /> using <see cref="Hardware.Close" />.
    /// </summary>
    public void Close()
    {
        _lmSensors?.Close();
        foreach (IHardware iHardware in SubHardware)
        {
            if (iHardware is Hardware hardware)
                hardware.Close();
        }
    }
}
