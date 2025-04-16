/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SocketCANSharp;
using SocketCANSharp.Network;

namespace ThingSet.Common.Transports.Can;

public delegate ValueTask AsyncEventHandler<TEventArgs>(object sender, TEventArgs args) where TEventArgs : EventArgs;

/// <summary>
/// Discovers other nodes on a CAN bus.
/// </summary>
public class PeerAddressListener : IDisposable
{
    private static readonly TimeSpan ObservationTimerInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ExceptionBackoffInterval = TimeSpan.FromSeconds(5);
    private const int ExceptionThreshold = 12;

    private readonly ThingSetCanInterface _canInterface;

    private readonly RawCanSocket _peerAddressListenerCanSocket;

    private readonly ConcurrentDictionary<byte, DateTime> _lastSeenByPeerAddress = new ConcurrentDictionary<byte, DateTime>();
    private readonly BlockingCollection<byte> _newPeers = new BlockingCollection<byte>(new ConcurrentQueue<byte>());

    private readonly Thread _peerAddressListenerThread;
    private readonly Thread _peerAddressDispatcherThread;
    private bool _runPeerAddressListener = true;

    private readonly Timer _peerObserverTimer;
    private bool _isDisappearanceObservationEnabled;

    public PeerAddressListener(ThingSetCanInterface canInterface)
    {
        _canInterface = canInterface;

        _peerAddressListenerCanSocket = new RawCanSocket
        {
            EnableCanFdFrames = canInterface.IsFdMode,
            CanFilters =
            [
                new CanFilter(canId: (uint)MessageType.MultiFrameReport, canMask: CanID.TypeMask),
                new CanFilter(canId: (uint)MessageType.SingleFrameReport, canMask: CanID.TypeMask),
            ]
        };
        _peerAddressListenerThread = new Thread(RunPeerAddressesHandler)
        {
            IsBackground = true,
        };
        _peerAddressDispatcherThread = new Thread(RunPeerAdressesDispatcher)
        {
            IsBackground = true,
        };

        _peerObserverTimer = new Timer(OnPeerObserverTimerCallback);
    }

    /// <summary>
    /// Event that is raised when a peer is discovered.
    /// </summary>
    public event AsyncEventHandler<AddressEventArgs>? PeerDiscovered;

    /// <summary>
    /// Event that is raised when a peer is not seen for more than the
    /// time specified in <see cref="DisappearedInterval"/>.
    /// </summary>
    public event AsyncEventHandler<AddressEventArgs>? PeerDisappeared;

    /// <summary>
    /// Specifies how long to wait before considering a peer to have
    /// disappared. The default value is 2 minutes.
    /// </summary>
    public TimeSpan DisappearedInterval { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets whether to watch for peers' disappearance, i.e.
    /// when they have stopped sending reports for a given amount
    /// of time.
    /// </summary>
    public bool IsDisappearanceObservationEnabled
    {
        get { return _isDisappearanceObservationEnabled; }
        set
        {
            if (_isDisappearanceObservationEnabled != value)
            {
                _isDisappearanceObservationEnabled = value;
                EnsureDisappearanceObservationTimer();
            }
        }
    }

    public void Dispose()
    {
        _runPeerAddressListener = false;
        if (_peerAddressListenerThread.IsAlive)
        {
            _peerAddressListenerThread.Join(1000);
        }
        _peerAddressListenerCanSocket.Dispose();
        _peerObserverTimer.Dispose();
    }

    /// <summary>
    /// Begin listening for peers.
    /// </summary>
    public void Listen()
    {
        _lastSeenByPeerAddress[_canInterface.NodeAddress] = DateTime.MaxValue; // add thyself and never timeout
        _peerAddressListenerCanSocket.Bind(_canInterface.Interface);
        _peerAddressListenerThread.Start();
        _peerAddressDispatcherThread.Start();
        EnsureDisappearanceObservationTimer();
    }

    public bool Forget(byte peerId)
    {
        return _lastSeenByPeerAddress.TryRemove(peerId, out _);
    }

    private async void RunPeerAdressesDispatcher()
    {
        foreach (byte peerId in _newPeers.GetConsumingEnumerable())
        {
            AddressEventArgs args = new AddressEventArgs(peerId);
            if (PeerDiscovered is not null)
            {
                await PeerDiscovered.Invoke(this, args);
            }
            if (args.Cancel)
            {
                Forget(peerId);
            }
        }
    }

    private void RunPeerAddressesHandler()
    {
        while (_runPeerAddressListener)
        {
            List<SocketCanException> exceptions = new List<SocketCanException>();
            try
            {
                if (_peerAddressListenerCanSocket.Read(out CanFrame readFrame) > 0)
                {
                    exceptions.Clear();
                    byte peerId = CanID.GetSource(readFrame.CanId);
                    if (peerId != CanID.BroadcastAddress && peerId != CanID.AnonymousAddress && peerId != _canInterface.NodeAddress)
                    {
                        if (_lastSeenByPeerAddress.TryAdd(peerId, DateTime.UtcNow))
                        {
                            _newPeers.Add(peerId);
                        }
                        else
                        {
                            _lastSeenByPeerAddress[peerId] = DateTime.UtcNow;
                        }
                    }
                }
            }
            catch (SocketCanException scex)
            {
                exceptions.Add(scex);
                if (exceptions.Count > ExceptionThreshold)
                {
                    throw new AggregateException($"Multiple errors occurred while reading from CAN interface {_canInterface.Interface.Name}.", exceptions);
                }
                Thread.Sleep(ExceptionBackoffInterval);
            }
        }
        _newPeers.CompleteAdding();
    }

    private void EnsureDisappearanceObservationTimer()
    {
        if (_isDisappearanceObservationEnabled)
        {
            _peerObserverTimer.Change(ObservationTimerInterval, ObservationTimerInterval);
        }
        else
        {
            _peerObserverTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    private async void OnPeerObserverTimerCallback(object? ignored)
    {
        foreach (byte peerAddress in _lastSeenByPeerAddress.Keys)
        {
            DateTime lastSeen = _lastSeenByPeerAddress[peerAddress];
            if (lastSeen < DateTime.UtcNow && DateTime.UtcNow - lastSeen > DisappearedInterval)
            {
                AddressEventArgs args = new AddressEventArgs(peerAddress);
                if (PeerDisappeared is not null)
                {
                    await PeerDisappeared.Invoke(this, args);
                }
                if (!args.Cancel)
                {
                    Forget(peerAddress);
                }
            }
        }
    }
}