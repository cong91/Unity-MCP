/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Unity-MCP)    │
│  Copyright (c) 2025 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/

#nullable enable
using System;
using System.IO;
using com.IvanMurzak.Unity.MCP.Editor.Utils;
using UnityEngine.UIElements;
using static com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;

namespace com.IvanMurzak.Unity.MCP.Editor.UI
{
    public class HermesConfigurator : AiAgentConfigurator
    {
        public override string AgentName => "Hermes Agent";
        public override string AgentId => "hermes-agent";
        public override string DownloadUrl => string.Empty;
        public override string? SkillsPath => Path.Combine(ProjectRootPath, ".hermes", "skills");
        public override string TutorialUrl => string.Empty;

        protected override string? IconFileName => null;

        private static string LocalConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".hermes",
            "config.yaml");

        protected override AiAgentConfig CreateConfigStdioWindows() => CreateConfigStdio();
        protected override AiAgentConfig CreateConfigStdioMacLinux() => CreateConfigStdio();
        protected override AiAgentConfig CreateConfigHttpWindows() => CreateConfigHttp();
        protected override AiAgentConfig CreateConfigHttpMacLinux() => CreateConfigHttp();

        private AiAgentConfig CreateConfigStdio()
        {
            return new YamlAiAgentConfig(
                name: AgentName,
                configPath: LocalConfigPath,
                bodyPath: "mcp_servers")
                .SetProperty("command", McpServerManager.ExecutableFullPath.Replace('\\', '/'), requiredForConfiguration: true, comparison: ValueComparisonMode.Path)
                .SetProperty("args", new object[] {
                    $"--{Args.Port}={UnityMcpPluginEditor.Port}",
                    $"--{Args.PluginTimeout}={UnityMcpPluginEditor.TimeoutMs}",
                    $"--{Args.ClientTransportMethod}={TransportMethod.stdio}",
                    $"--{Args.Authorization}={UnityMcpPluginEditor.AuthOption}"
                }, requiredForConfiguration: true)
                .SetProperty("connect_timeout", 120)
                .SetProperty("timeout", 120)
                .RequireListContains("skills.external_dirs", SkillsPath!.Replace('\\', '/'), requiredForConfiguration: true, comparison: ValueComparisonMode.Path)
                .SetPropertyToRemove("url")
                .SetPropertyToRemove("bearer_token");
        }

        private AiAgentConfig CreateConfigHttp()
        {
            return new YamlAiAgentConfig(
                name: AgentName,
                configPath: LocalConfigPath,
                bodyPath: "mcp_servers")
                .SetProperty("url", UnityMcpPluginEditor.Host, requiredForConfiguration: true, comparison: ValueComparisonMode.Url)
                .SetProperty("connect_timeout", 120)
                .SetProperty("timeout", 120)
                .RequireListContains("skills.external_dirs", SkillsPath!.Replace('\\', '/'), requiredForConfiguration: true, comparison: ValueComparisonMode.Path)
                .SetPropertyToRemove("command")
                .SetPropertyToRemove("args");
        }

        protected override void OnUICreated(VisualElement root)
        {
            base.OnUICreated(root);

            var configPathDisplay = LocalConfigPath.Replace('\\', '/');
            var skillsPathDisplay = SkillsPath!.Replace('\\', '/');

            var stdioManual = TemplateFoldoutFirst("Manual Configuration Steps");
            stdioManual!.Add(TemplateLabelDescription("1. Open or create Hermes config file."));
            stdioManual.Add(TemplateTextFieldReadOnly(configPathDisplay));
            stdioManual.Add(TemplateLabelDescription("2. Copy the YAML below into the config file."));
            stdioManual.Add(TemplateTextFieldReadOnly(ConfigStdio.ExpectedFileContent));
            stdioManual.Add(TemplateLabelDescription("3. Restart Hermes after configuration changes."));
            ContainerStdio!.Add(stdioManual);

            var stdioTroubleshooting = TemplateFoldout("Troubleshooting");
            stdioTroubleshooting!.Add(TemplateLabelDescription("- Hermes reads MCP servers from ~/.hermes/config.yaml"));
            stdioTroubleshooting.Add(TemplateLabelDescription($"- Generated Unity skills are expected under: {skillsPathDisplay}"));
            stdioTroubleshooting.Add(TemplateLabelDescription("- Ensure skills.external_dirs contains the Unity project skills path"));
            ContainerStdio!.Add(stdioTroubleshooting);

            var httpManual = TemplateFoldoutFirst("Manual Configuration Steps");
            httpManual!.Add(TemplateLabelDescription("1. Open or create Hermes config file."));
            httpManual.Add(TemplateTextFieldReadOnly(configPathDisplay));
            httpManual.Add(TemplateLabelDescription("2. Copy the YAML below into the config file."));
            httpManual.Add(TemplateTextFieldReadOnly(ConfigHttp.ExpectedFileContent));
            httpManual.Add(TemplateLabelDescription("3. Restart Hermes after configuration changes."));
            ContainerHttp!.Add(httpManual);

            var httpTroubleshooting = TemplateFoldout("Troubleshooting");
            httpTroubleshooting!.Add(TemplateLabelDescription("- Hermes reads MCP servers from ~/.hermes/config.yaml"));
            httpTroubleshooting.Add(TemplateLabelDescription($"- Generated Unity skills are expected under: {skillsPathDisplay}"));
            httpTroubleshooting.Add(TemplateLabelDescription("- Ensure skills.external_dirs contains the Unity project skills path"));
            ContainerHttp!.Add(httpTroubleshooting);
        }
    }
}
