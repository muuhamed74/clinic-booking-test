using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Clinic.Service.Hubs
{
    public class NotificationHub : Hub
    {
        private static readonly ConcurrentDictionary<int, HashSet<string>> _patientConnections = new();

        public Task RegisterPatient(int patientId)
        {
            _patientConnections.AddOrUpdate(
                patientId,
                _ => new HashSet<string> { Context.ConnectionId },
                (_, connections) => {
                    lock (connections)
                    {
                        connections.Add(Context.ConnectionId);
                    }
                    return connections;
                });
            return Task.CompletedTask;
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            foreach (var kvp in _patientConnections)
            {
                lock (kvp.Value)
                {
                    kvp.Value.Remove(Context.ConnectionId);
                    if (kvp.Value.Count == 0)
                    {
                        _patientConnections.TryRemove(kvp.Key, out _);
                    }
                }
            }
            return base.OnDisconnectedAsync(exception);
        }

        public static IReadOnlyCollection<string>? GetConnections(int patientId)
        {
            if (_patientConnections.TryGetValue(patientId, out var connections))
            {
                lock (connections)
                {
                    return connections.ToList();
                }
            }
            return null;
        }




    }
}
