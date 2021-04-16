﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Events;
using Microsoft.DotNet.Interactive.Formatting;

namespace Polyglot.Core
{
    public class GameEngineClient
    {
        private GameStatus _gameStatus;
        private readonly HttpClient _client;
        private DateTime? _lastRun;
        private readonly Dictionary<string, IMetricCalculator> _metrics = new ();
        public string GameId { get; }
        public string UserId { get; }
        public string Password { get; }
        public string PlayerId { get; }
        public string ServerUrl { get; }

        private GameEngineClient(string gameId, string userId, string password, string playerId, string serverUrl,
            HttpClient httpClient)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(gameId));
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(password));
            }

            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(playerId));
            }

            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(serverUrl));
            }

            GameId = gameId;
            UserId = userId;
            Password = password;
            PlayerId = playerId;
            ServerUrl = serverUrl;
            _client = httpClient;
            
        }

        public void AddMetric(string metricId, IMetricCalculator metric)
        {
            _metrics[metricId] = metric;
        }

        public static GameEngineClient Current { get; set; }

        public static void Configure(string gameId, string userId, string password, string playerId,
            string serverUrl = null, Func<HttpClient> clientFactory = null)
        {
            Current = new GameEngineClient(gameId, userId, password, playerId,
                string.IsNullOrWhiteSpace(serverUrl) ? DefaultServerUrl : serverUrl, clientFactory?.Invoke() ?? new HttpClient());
        }

        public static string DefaultServerUrl { get; } = "https://dev.smartcommunitylab.it/gamification-v3/";

        public async Task<GameStateReport> SubmitActions(SubmitCode command, Kernel kernel,
            List<KernelEvent> events, IReadOnlyDictionary<string, string> newVariables, TimeSpan runTime)
        {

            EnsureAuthentication();

            var callUrl = new Uri(ServerUrl);
            var action = "SubmitCode";

            callUrl = new Uri(callUrl, $"exec/game/{GameId}/action/{action}");

            // find required metrics for current stage
            var metrics = await GetMetricsAsync();

            var data = new Dictionary<string, object>();

            // compile data using the values calculated for the required metrics
            foreach (var metric in metrics)
            {
                if (_metrics.TryGetValue(metric, out var metricCalculator))
                {
                    data[metric] =
                        await metricCalculator.CalculateAsync(command, kernel, events, newVariables, runTime, _lastRun);
                }
                else
                {
                    data[metric] = "not found";
                }
            }

            var bodyObject = new
            {
                gameId = GameId,
                playerId = PlayerId,
                data = data
            };

            var response = await _client.PostAsync(callUrl, bodyObject.ToBody());

            if (response.StatusCode == HttpStatusCode.OK)
            {
                _lastRun = DateTime.Now;
                return await GetReportAsync();
            }

            var formattedValues = new ImmutableArray<FormattedValue>
            {
                new(PlainTextFormatter.MimeType, response.ReasonPhrase)
            };

            KernelInvocationContext.Current?.Publish(new ErrorProduced(response.ReasonPhrase,
                KernelInvocationContext.Current?.Command, formattedValues));

            return null;
        }

        private  Task<string[]> GetMetricsAsync()
        {
            return Task.FromResult(new []{ "timeSpent" , "warnings", "errors", "newVariables", "timeSinceLastAction", "success", "declaredClasses", "declarationsStructure" });
        }

        private void EnsureAuthentication()
        {
            var authToken = Encoding.ASCII.GetBytes($"{UserId}:{Password}");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(authToken));
        }

        public async Task<GameStateReport> GetReportAsync()
        {
            var callUrlStatus = new Uri(ServerUrl);
            callUrlStatus = new Uri(callUrlStatus, $"data/game/{GameId}/player/{PlayerId}");

            var responseStatus = await _client.GetAsync(callUrlStatus);
            if (responseStatus.StatusCode == HttpStatusCode.OK)
            {
                // retrieve the player status from the GET call response and print it

                var contents = await responseStatus.Content.ReadAsStringAsync();

                var gameStatus = contents.ToObject<GameStatus>();

                // report the new status to the player

                _gameStatus = gameStatus;

                var scoring = gameStatus.State.PointConcept.ToDictionary(p => p.Name);

                return new GameStateReport(_gameStatus.CustomData.Level,
                    scoring["points"].Score,
                    scoring["gold coins"].Score);
            }

            throw new InvalidCastException(
                $"Failed Game Engine Step, Code: {responseStatus.StatusCode}, Reason: {responseStatus.ReasonPhrase}");
        }

        public static void Reset()
        {
            Current = null;
        }
    }

}