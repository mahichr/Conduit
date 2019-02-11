﻿using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

namespace Archetypical.Software.Conduit
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TFilter"></typeparam>
    public class Conduit<TFilter> : IConduit where TFilter : class, new()
    {
        #region ConnectionFilterPair

        /// <summary>
        /// 
        /// </summary>
        protected class ConnectionFilterPair
        {
            public ConnectionFilterPair(string connectionId, TFilter filter)
            {
                ConnectionId = connectionId;
                Filter = filter;
            }

            public string ConnectionId { get; }
            public TFilter Filter { get; set; }
        }

        #endregion

        private static Conduit _conduit;
        private static ConcurrentDictionary<string, ConnectionFilterPair> ConnectionFilterMap = new ConcurrentDictionary<string, ConnectionFilterPair>();

        private IConduitFilter<TFilter> FilterFactory { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filterFactory"></param>
        /// <param name="conduit"></param>
        public Conduit(IConduitFilter<TFilter> filterFactory, Conduit conduit)
        {
            FilterFactory = filterFactory;
            _conduit = conduit;

            var eventKey = typeof(TFilter).Name;

            _conduit.FilterActions.Add(eventKey, (dynamic, connectionId) =>
            {
                var mappedFilter = Mapper<TFilter>.Map(dynamic) as TFilter;
                var pair = new ConnectionFilterPair(connectionId, mappedFilter);
                var current = ConnectionFilterMap.AddOrUpdate(connectionId, pair, (x,y) => pair);
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        public void OnContextConnectedAsync(HubCallerContext context)
        {
            if (FilterFactory != null)
            {
                var filter = FilterFactory.PopulateClient(context);
                var pair = new ConnectionFilterPair(context.ConnectionId, filter ?? new TFilter());
                ConnectionFilterMap.TryAdd(context.ConnectionId, pair);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        public void OnContextDisconnectedAsync(HubCallerContext context)
        {
            ConnectionFilterMap.TryRemove(context.ConnectionId, out ConnectionFilterPair value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventKey"></param>
        /// <param name="payload"></param>
        /// <param name="clientSelector"></param>
        /// <returns></returns>
        public static Task SendAsync<TPayload>(Predicate<TFilter> clientSelector, TPayload payload)
        {
            var filterType = typeof(TFilter).Name;
            if(!_conduit.FilterActions.ContainsKey(filterType))
            {
                throw new NotSupportedException($"There is no Conduit<{filterType}> registered on the server");
            }

            IEnumerable<ConnectionFilterPair> query = ConnectionFilterMap.Values;

            var ids = query.Where(x => clientSelector(x.Filter)).Select(x => x.ConnectionId).ToList();

            if (ids.Any())
            {
                return _conduit.Clients?.Clients(ids).SendAsync(typeof(TPayload).Name, payload);
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class Conduit : Hub
    {
        protected internal Dictionary<string, Action<dynamic, string>> FilterActions = new Dictionary<string, Action<dynamic, string>>(StringComparer.CurrentCultureIgnoreCase);

        internal List<IConduit> Children = new List<IConduit>();

        /// <summary>
        /// 
        /// </summary>
        public Conduit() { }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override Task OnConnectedAsync()
        {
            Children.ForEach(conduit => conduit.OnContextConnectedAsync(Context));
            return base.OnConnectedAsync();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        public override Task OnDisconnectedAsync(Exception exception)
        {
            Children.ForEach(conduit => conduit.OnContextDisconnectedAsync(Context));
            return base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filterName"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public void ApplyFilter(string filterName, ExpandoObject filter)
        {
            if (FilterActions.ContainsKey(filterName))
            {
                var del = FilterActions[filterName];
                del.Invoke(filter, Context.ConnectionId);
            }
            else
            {
                throw new NotSupportedException($"There is no Conduit<{filterName}> registered on the server");
            }
        }
    }
}