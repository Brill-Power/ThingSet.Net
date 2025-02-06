/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using SocketCANSharp;
using SocketCANSharp.Network;

namespace ThingSet.Common.Transports.Can;

public class ThingSetCanInterface : IDisposable
{
    private readonly CanNetworkInterface _canInterface;

    public static readonly byte LocalBridge = CanID.LocalBridge;

    private static readonly byte[] MacAddress = new byte[8];
    private static readonly byte[] Zero = new byte[0];

    private readonly RawCanSocket _addressClaimerCanSocket;

    protected byte _nodeAddress;

    private readonly Thread _addressClaimerThread;
    private bool _runAddressClaimer = true;

    static ThingSetCanInterface()
    {
        // for now, take the MAC address of the first non-loopback interface to use as the station ID
        byte[] macAddress = NetworkInterface.GetAllNetworkInterfaces().Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback).First().GetPhysicalAddress().GetAddressBytes();
        Buffer.BlockCopy(macAddress, 0, MacAddress, 0, macAddress.Length);
    }

    public ThingSetCanInterface(string canInterfaceName)
    {
        CanNetworkInterface? canInterface = null;
        foreach (CanNetworkInterface can in CanNetworkInterface.GetAllInterfaces(true))
        {
            if (String.Equals(can.Name, canInterfaceName, StringComparison.InvariantCultureIgnoreCase))
            {
                canInterface = can;
                break;
            }
        }

        if (canInterface is null)
        {
            throw new ArgumentException($"Could not find CAN interface {canInterfaceName}");
        }

        _canInterface = canInterface;

        _addressClaimerCanSocket = new RawCanSocket
        {
            EnableCanFdFrames = IsFdMode,
        };
        _addressClaimerThread = new Thread(RunAddressClaimer)
        {
            IsBackground = true,
        };
    }

    public CanNetworkInterface Interface => _canInterface;

    public int FrameSize => IsFdMode ? 64 : 8;

    /// <summary> Gets the negotiated address. </summary>
    public byte NodeAddress => _nodeAddress;

    public bool IsFdMode => Interface.MaximumTransmissionUnit == SocketCanConstants.CANFD_MTU;

    /// <summary>Negotiates a unique node address on the attached CAN interface.</summary>
    public async ValueTask BindAsync()
    {
        await BindAsync(0x01);
    }

    /// <summary>
    /// Negotiates a unique node address on the attached CAN interface.
    /// </summary>
    /// <param name="nodeAddress">Try to claim the specified address, if available.</param>
    public async ValueTask BindAsync(byte nodeAddress)
    {
        Console.WriteLine($"Connecting using interface {_canInterface.Name}");
        Random dice = new Random();
        _nodeAddress = nodeAddress;
        using (RawCanSocket rawCanSocket = new RawCanSocket
        {
            EnableCanFdFrames = IsFdMode,
            CanFilters =
            [
                new CanFilter(canId: (uint)MessageType.Network | CanID.SetTarget(CanID.BroadcastAddress),
                    canMask: CanID.TypeMask | CanID.TargetMask),
            ],
        })
        {
            bool read = true;
            rawCanSocket.Bind(_canInterface);
            BlockingCollection<CanFrame> canFrames = new BlockingCollection<CanFrame>(new ConcurrentQueue<CanFrame>());

            Thread thread = new Thread(_ =>
            {
                while (read)
                {
                    if (rawCanSocket.Read(out CanFrame readFrame) > 0)
                    {
                        canFrames.Add(readFrame);
                    }
                }
            })
            {
                IsBackground = true,
            };
            thread.Start();

            while (true)
            {
                byte rand = (byte)(dice.Next(Byte.MaxValue) & 0xff);
                try
                {
                    uint canId = CanID.CreateCanID(MessageType.Network, MessagePriority.NetworkManagement, CanID.AnonymousAddress, _nodeAddress) | CanID.SetRandomElement(rand);
                    if (IsFdMode)
                    {
                        CanFdFrame canFrame = new CanFdFrame(canId, Zero, CanFdFlags.None);
                        rawCanSocket.Write(canFrame);
                    }
                    else
                    {
                        CanFrame canFrame = new CanFrame(canId: canId, data: Zero);
                        rawCanSocket.Write(canFrame);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                    continue;
                }

                if (canFrames.TryTake(out CanFrame readFrame, TimeSpan.FromMilliseconds(500)) &&
                    _nodeAddress == CanID.GetSource(readFrame.CanId))
                {
                    // address already in use
                    byte previousNodeAddress = _nodeAddress;
                    _nodeAddress = (byte)(1 + dice.Next() % CanID.MaxAddress);
                    Console.WriteLine($"Node address {previousNodeAddress:x} is already in use; trying {_nodeAddress:x}");
                }
                else
                {
                    // send claim
                    if (!TryClaimAddress(rawCanSocket))
                    {
                        continue;
                    }

                    Console.WriteLine($"Using node address {_nodeAddress:x}");
                    read = false;
                    break;
                }
            }
        }

        _addressClaimerCanSocket.CanFilters =
        [
            new CanFilter(
                canId: (uint)MessageType.Network | CanID.SetSource(CanID.AnonymousAddress) | CanID.SetTarget(_nodeAddress),
                canMask: CanID.TypeMask | CanID.TargetMask | CanID.SourceMask),
        ];
        _addressClaimerCanSocket.Bind(_canInterface);
        _addressClaimerThread.Start();
    }

    protected virtual void Dispose(bool disposing)
    {
        _runAddressClaimer = false;
        if (_addressClaimerThread.IsAlive)
        {
            _addressClaimerThread.Join(1000);
        }
        _addressClaimerCanSocket.Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void RunAddressClaimer()
    {
        while (_runAddressClaimer)
        {
            if (_addressClaimerCanSocket.Read(out CanFrame readFrame) > 0)
            {
                TryClaimAddress(_addressClaimerCanSocket);
            }
        }
    }

    private bool TryClaimAddress(RawCanSocket rawCanSocket)
    {
        // send claim
        Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] Claiming address {_nodeAddress:x}");
        uint canId = CanID.CreateCanID(MessageType.Network, MessagePriority.NetworkManagement, _nodeAddress, CanID.BroadcastAddress);
        try
        {
            if (IsFdMode)
            {
                CanFdFrame canFrame = new CanFdFrame(canId, MacAddress, CanFdFlags.None);
                rawCanSocket.Write(canFrame);
            }
            else
            {
                CanFrame canFrame = new CanFrame(canId: canId, MacAddress);
                rawCanSocket.Write(canFrame);
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine($"Address claim failed");
            return false;
        }
    }

    public static bool IsValidAddress(byte nodeAddress)
    {
        return nodeAddress >= CanID.MinAddress && nodeAddress <= CanID.MaxAddress;
    }
}