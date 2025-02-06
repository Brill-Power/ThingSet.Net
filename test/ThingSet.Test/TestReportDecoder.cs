/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using NUnit.Framework.Legacy;
using ThingSet.Common.Protocols;
using ThingSet.Common.Protocols.Binary;

namespace ThingSet.Test;

public class TestReportDecoder
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void TestSimpleDatum()
    {
        ReportDecoder<SupercellDatum> decoder = new ReportDecoder<SupercellDatum>();
        Memory<byte> buffer = new Memory<byte>(
            [0xA2,
                0x19, 0x10, 0x01,
                0xFA, 0x3F, 0x9D, 0x70, 0xA4,
                0x19, 0x10, 0x02,
                0xFA, 0x40, 0x91, 0xEB, 0x85
            ]);
        Assert.That(decoder.TryDecode(buffer, out SupercellDatum? datum));
        ClassicAssert.NotNull(datum);
        Assert.That(datum.StateOfCharge, Is.EqualTo(1.23f));
        Assert.That(datum.StateOfHealth, Is.EqualTo(4.56f));
    }

    [Test]
    public void TestWholeShebang()
    {
        ReportDecoder<ModuleDatum> decoder = new ReportDecoder<ModuleDatum>();
        Memory<byte> buffer = new Memory<byte>(
            [0xA6,
                0x19, 0x01, 0x01, 0x1B, 0x48, 0xAF, 0x70, 0xB5, 0x6D, 0x79, 0x65, 0x46,
                0x19, 0x01, 0x02, 0x01,
                0x19, 0x02, 0x00, 0xFA, 0x3F, 0x9D, 0x70, 0xA4,
                0x19, 0x02, 0x01, 0xFA, 0x40, 0x91, 0xEB, 0x85,
                0x19, 0x05, 0x00, 0x82, 0xFA, 0x3F, 0x9D, 0x70, 0xA4, 0xFA, 0x40, 0x91, 0xEB, 0x85,
                0x19, 0x10, 0x00,
                    0x82,
                        0xA2,
                            0x19, 0x10, 0x01,
                            0xFA, 0x3F, 0x9D, 0x70, 0xA4,
                            0x19, 0x10, 0x02,
                            0xFA, 0x40, 0x91, 0xEB, 0x85,
                        0xA2,
                            0x19, 0x10, 0x01,
                            0xFA, 0x3F, 0x9D, 0x70, 0xA4,
                            0x19, 0x10, 0x02,
                            0xFA, 0x40, 0x91, 0xEB, 0x85
            ]);
        Assert.That(decoder.TryDecode(buffer, out ModuleDatum? datum));
        ClassicAssert.NotNull(datum);
        Assert.That(datum.Voltage, Is.EqualTo(1.23f));
        Assert.That(datum.Current, Is.EqualTo(4.56f));
        Assert.That(datum.CellVoltages, Is.Not.Null);
        Assert.That(datum.CellVoltages.Length, Is.EqualTo(2));
        Assert.That(datum.CellVoltages[0], Is.EqualTo(1.23f));
        Assert.That(datum.CellVoltages[1], Is.EqualTo(4.56f));
        Assert.That(datum.SupercellData, Is.Not.Null);
        Assert.That(datum.SupercellData.Length, Is.EqualTo(2));
        Assert.That(datum.SupercellData[0].StateOfCharge, Is.EqualTo(1.23f));
    }

    public class SupercellDatum
    {
        [ThingSetReportField(0x1001)]
        public float StateOfCharge { get; set; }
        [ThingSetReportField(0x1002)]
        public float StateOfHealth { get; set; }
    }

    public class ModuleDatum
    {
        [ThingSetReportField(0x101)]
        public ulong Eui { get; set; }
        [ThingSetReportField(0x102)]
        public uint Hri { get; set; }
        [ThingSetReportField(0x200)]
        public float Voltage { get; set; }
        [ThingSetReportField(0x201)]
        public float Current { get; set; }
        [ThingSetReportField(0x500)]
        public float[]? CellVoltages { get; set; }
        [ThingSetReportField(0x1000)]
        public SupercellDatum[]? SupercellData { get; set; }
    }
}