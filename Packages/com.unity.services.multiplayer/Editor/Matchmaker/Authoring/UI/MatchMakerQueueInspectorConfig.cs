using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Unity.Services.Multiplayer.Editor.Matchmaker.Authoring.Core.Model;
using Unity.Services.Multiplayer.Editor.Matchmaker.Authoring.IO;
using UnityEngine;
using static Unity.Services.Multiplayer.Editor.Matchmaker.Authoring.Core.Model.IMatchHostingConfig;

namespace Unity.Services.Multiplayer.Editor.Matchmaker.Authoring.UI
{
    // Data models for the Matchmaker Queue configuration
    [Serializable]
    class MatchmakerQueueData
    {
        [JsonProperty(PropertyName = "$schema")]
        public string Schema;
        public string Name;
        public bool Enabled;
        public int MaxPlayersPerTicket;
        public DefaultPool DefaultPool;
    }

    [Serializable]
    class DefaultPool
    {
        public List<object> Variants = new();
        public string Name;
        public bool Enabled;
        public int TimeoutSeconds;
        public MatchLogic MatchLogic;
        public MatchHosting MatchHosting;
    }

    [Serializable]
    class MatchLogic
    {
        public MatchDefinition MatchDefinition;
        public string Name;
        public bool BackfillEnabled;
    }

    [Serializable]
    class MatchDefinition
    {
        public List<Team> Teams;
        // This property is used to store the json file in the editor serialization so it is not lost when doing match definition changes.
        public string MatchRulesJson;
        public List<Rule> MatchRules;
    }

    [Serializable]
    class Team
    {
        public string Name;
        public TeamCount TeamCount;
        public PlayerCount PlayerCount;
        // This property is used to store the json file in the editor serialization so it is not lost when doing match definition changes.
        public string TeamRulesJson;
        public List<Rule> TeamRules;
    }

    [Serializable]
    class TeamCount
    {
        public int Min;
        public int Max;
        public List<RangeRelaxation> Relaxations = new();
    }

    [Serializable]
    class PlayerCount
    {
        public int Min;
        public int Max;
        public List<RangeRelaxation> Relaxations = new();
    }

    [Serializable]
    class RangeRelaxation
    {
        public RangeRelaxationType type;
        public AgeType ageType;
        public double atSeconds;
        public double value;
    }

    [Serializable]
    class MatchHosting
    {
        public enum MatchHostingType
        {
            [EnumMember(Value = "MatchId")]
            Client = 1
        };

        [JsonConverter(typeof(StringEnumConverter))]
        public MatchHostingType Type;
        public string FleetName;
        public string BuildConfigurationName;
        public string DefaultQoSRegionName;
    }

    [Serializable]
    class MatchMakerQueueInspectorConfig : ScriptableObject
    {
        public MatchmakerQueueData Data;

        public void Initialize(QueueConfig queueConfig)
        {
            if (queueConfig == null)
            {
                Debug.LogError("Cannot initialize MatchMakerQueueConfigContainer with null queueConfig");
                return;
            }

            Data = new MatchmakerQueueData
            {
                Schema = queueConfig.Schema,
                Name = queueConfig.Name.ToString(),
                Enabled = queueConfig.Enabled,
                MaxPlayersPerTicket = queueConfig.MaxPlayersPerTicket,
                DefaultPool = new DefaultPool
                {
                    Name = queueConfig.DefaultPool.Name.ToString(),
                    Enabled = queueConfig.DefaultPool.Enabled,
                    TimeoutSeconds = queueConfig.DefaultPool.TimeoutSeconds,
                    MatchLogic = new MatchLogic
                    {
                        Name = queueConfig.DefaultPool.MatchLogic.Name,
                        BackfillEnabled = queueConfig.DefaultPool.MatchLogic.BackfillEnabled,
                        MatchDefinition = new MatchDefinition
                        {
                            Teams = ConvertTeams(queueConfig.DefaultPool.MatchLogic.MatchDefinition.teams),
                            MatchRulesJson = JsonConvert.SerializeObject(queueConfig.DefaultPool.MatchLogic.MatchDefinition.matchRules, Formatting.None, MatchmakerConfigLoader.GetSerializationSettings())
                        }
                    },
                    MatchHosting = new MatchHosting
                    {
                        Type = ConvertMatchHostingType(queueConfig.DefaultPool.MatchHosting.Type),
                        FleetName = string.Empty,
                        BuildConfigurationName = string.Empty,
                        DefaultQoSRegionName = string.Empty
                    }
                }
            };
        }

        internal List<Team> ConvertTeams(List<RuleBasedTeamDefinition> sourceTeams)
        {
            if (sourceTeams == null)
            {
                return new List<Team>();
            }

            var teams = new List<Team>();
            foreach (var sourceTeam in sourceTeams)
            {
                var newTeam = new Team()
                {
                    Name = sourceTeam.name,
                    TeamCount = new TeamCount
                    {
                        Min = sourceTeam.teamCount.min,
                        Max = sourceTeam.teamCount.max,
                        Relaxations = new List<RangeRelaxation>(),
                    },
                    PlayerCount = new PlayerCount
                    {
                        Min = sourceTeam.playerCount.min,
                        Max = sourceTeam.playerCount.max,
                        Relaxations = new List<RangeRelaxation>(),
                    },
                    TeamRulesJson = JsonConvert.SerializeObject(sourceTeam.teamRules, Formatting.None, MatchmakerConfigLoader.GetSerializationSettings())
                };

                if (sourceTeam.teamCount.relaxations != null)
                {
                    foreach (var sourceTeamRule in sourceTeam.teamCount.relaxations)
                    {
                        newTeam.TeamCount.Relaxations.Add(new RangeRelaxation()
                        {
                            ageType = sourceTeamRule.ageType,
                            atSeconds = sourceTeamRule.atSeconds,
                            type = sourceTeamRule.type,
                            value = sourceTeamRule.value,
                        });
                    }
                }

                if (sourceTeam.playerCount.relaxations != null)
                {
                    foreach (var sourceTeamRule in sourceTeam.playerCount.relaxations)
                    {
                        newTeam.PlayerCount.Relaxations.Add(new RangeRelaxation()
                        {
                            ageType = sourceTeamRule.ageType,
                            atSeconds = sourceTeamRule.atSeconds,
                            type = sourceTeamRule.type,
                            value = sourceTeamRule.value,
                        });
                    }
                }
                teams.Add(newTeam);
            }
            return teams;
        }

        internal MatchHosting.MatchHostingType ConvertMatchHostingType(MatchHostingType sourceType)
        {
            return MatchHosting.MatchHostingType.Client;
        }

        // Called when trying to serialize the matchmaker queue inspector data back to json.
        // This updates the expected non-unity-serialized list of rules back into the right format before the json serialization.
        public string ToJson(JsonSerializerSettings serializer)
        {
            Data.DefaultPool.MatchLogic.MatchDefinition.MatchRules = JsonConvert.DeserializeObject<List<Rule>>(
                Data.DefaultPool.MatchLogic.MatchDefinition.MatchRulesJson,
                MatchmakerConfigLoader.GetSerializationSettings());

            foreach (var team in Data.DefaultPool.MatchLogic.MatchDefinition.Teams)
            {
                team.TeamRules = JsonConvert.DeserializeObject<List<Rule>>(
                    team.TeamRulesJson,
                    MatchmakerConfigLoader.GetSerializationSettings());
            }

            return JsonConvert.SerializeObject(Data, serializer);
        }
    }
}
