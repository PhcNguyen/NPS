﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NServer.Core.Packets;
using NServer.Core.Session;
using NServer.Application.Handler;
using NServer.Infrastructure.Logging;
using NServer.Infrastructure.Services;
using NServer.Core.Interfaces.Session;
using System.Collections.Generic;

namespace NServer.Application.Main
{
    internal class PacketProcessor(SessionManager sessionManager, CancellationToken cancellationToken)
    {
        private readonly PacketReceiver _receiverContainer = Singleton.GetInstance<PacketReceiver>();
        private readonly PacketSender _senderContainer = Singleton.GetInstance<PacketSender>();

        private readonly SessionManager _sessionManager = sessionManager;
        private readonly TaskManager _taskManager = new(100); // Giới hạn 100 tác vụ đồng thời

        private readonly CancellationToken _cancellationToken = cancellationToken;

        // Public method to start processing both incoming and outgoing packets
        public void StartProcessing()
        {
            _ = Task.Run(async () => await ProcessIncomingPackets(), _cancellationToken);
            _ = Task.Run(async () => await ProcessOutgoingPackets(), _cancellationToken);
        }

        // Private method to process incoming packets
        private async Task ProcessIncomingPackets()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                if (_receiverContainer.Count() == 0)
                {
                    await Task.Delay(50, _cancellationToken);
                    continue;
                }

                List<Packet> packetsBatch = _receiverContainer.DequeueBatch(50);

                IEnumerable<Func<Task>> tasks = packetsBatch.Select(packet => (Func<Task>)(async () =>
                {
                    try
                    {
                        Packet responsePacket = await CommandDispatcher.HandleCommand(packet).ConfigureAwait(false);
                
                        _senderContainer.AddPacket(responsePacket);
                    }
                    catch (Exception ex)
                    {
                        NLog.Instance.Error($"Error processing packet: {ex.Message}");
                    }
                }));

                await _taskManager.ExecuteTasksAsync(tasks, _cancellationToken);
            }
        }

        // Private method to process outgoing packets
        private async Task ProcessOutgoingPackets()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                if (_senderContainer.Count() == 0)
                {
                    await Task.Delay(50, _cancellationToken);
                    continue;
                }

                List<Packet> packetsBatch = _senderContainer.DequeueBatch(50);

                IEnumerable<Func<Task>> tasks = packetsBatch.Select(packet => (Func<Task>)(async () =>
                {
                    ISessionClient? session = _sessionManager.GetSession(packet.Id);

                    if (session == null || !session.IsConnected) return;

                    try
                    {
                        session.UpdateLastActivityTime();

                        if (packet.Payload.Length == 0) return;

                        await session.Send(packet);
                    }
                    catch (Exception ex)
                    {
                        NLog.Instance.Error($"Error sending packet: {ex.Message}");
                    }
                }));

                await _taskManager.ExecuteTasksAsync(tasks, _cancellationToken);
            }
        }

        // New method to get the count of incoming packets
        public int GetIncomingPacketCount()
        {
            return _receiverContainer.Count();
        }

        // New method to get the count of outgoing packets
        public int GetOutgoingPacketCount()
        {
            return _senderContainer.Count();
        }
    }
}