using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace UnityMCP.Editor
{
    /// <summary>
    /// The Lab: Autonomous Experimentation & Optimization Framework.
    ///
    /// This is the next radical capability after persistent memory.
    /// It allows AI agents to run rigorous, data-driven experiments on game mechanics,
    /// parameter sweeps, A/B tests, and balance changes — grounded against the
    /// Living Design Document and accumulated insights in project memory.
    ///
    /// This turns game development from "the AI makes changes and hopes they feel good"
    /// into "the AI runs science on the game and evolves it toward defined quality."
    /// </summary>
    public class LabTool : McpToolBase
    {
        public override string ToolName => "lab";
        public override string Description => "Autonomous experimentation and optimization lab. Define experiments, run batches of instrumented play sessions, measure results against project memory goals, and evolve mechanics with data.";
        public override string Category => "lab";

        [Serializable]
        public class LabParams
        {
            public string action = "status";

            // For create_experiment
            public string experimentName;
            public string hypothesis;
            public string[] parameters;           // e.g. ["PlayerController.jumpForce", "PlayerController.gravityScale"]
            public float[][] valueRanges;         // parallel to parameters, e.g. [[8.0, 12.0], [1.5, 3.0]]
            public int trialCount = 12;
            public float trialDuration = 8f;
            public string[] inputSequences;       // high-level input scripts the trials will run

            // For run / analyze
            public string experimentId;
        }

        public override object Execute(object parameters)
        {
            try
            {
                var args = GetParameters<LabParams>(parameters);
                var action = (args.action ?? "status").Trim().ToLowerInvariant();

                return action switch
                {
                    "status" => GetLabStatus(),
                    "create_experiment" => CreateExperiment(args),
                    "list_experiments" => ListExperiments(),
                    "run_trials" => RunTrials(args),
                    "analyze_results" => AnalyzeResults(args),
                    "apply_champion" => ApplyChampion(args),
                    _ => CreateErrorResponse($"Unknown lab action '{args.action}'")
                };
            }
            catch (Exception e)
            {
                LogError($"Lab operation failed: {e.Message}");
                return CreateErrorResponse($"Lab failed: {e.Message}", e.StackTrace);
            }
        }

        private object GetLabStatus()
        {
            // In a full implementation this would scan a Lab/ folder for previous experiments
            return CreateSuccessResponse(new
            {
                status = "ready",
                message = "UnityMCP Lab is active. Use lab.create_experiment to start a data-driven optimization run grounded in your project's memory and design goals.",
                capabilities = new[]
                {
                    "Define parameter experiments with clear hypotheses",
                    "Run batches of automated, instrumented play sessions",
                    "Measure against living design intent stored in memory",
                    "Evolve and apply winning variants with full audit trail"
                }
            }, "Lab status");
        }

        private object CreateExperiment(LabParams args)
        {
            if (string.IsNullOrWhiteSpace(args.experimentName))
                return CreateErrorResponse("experimentName is required");

            // In the real implementation we would persist this experiment definition
            // into Assets/Editor/UnityMCP/Lab/Experiments/{id}.json
            // and link it to the current memory state.

            var experimentId = Guid.NewGuid().ToString("N").Substring(0, 8);

            return CreateSuccessResponse(new
            {
                experimentId,
                name = args.experimentName,
                hypothesis = args.hypothesis,
                parameters = args.parameters,
                trialCount = args.trialCount,
                status = "defined",
                nextStep = "Call lab.run_trials with this experimentId to execute the experiment using playmode.observe under the hood."
            }, $"Experiment '{args.experimentName}' created");
        }

        private object ListExperiments()
        {
            // Placeholder - would read from disk
            return CreateSuccessResponse(new
            {
                experiments = new object[] { },
                message = "No experiments recorded yet. Create one with lab.create_experiment."
            });
        }

        private object RunTrials(LabParams args)
        {
            if (string.IsNullOrWhiteSpace(args.experimentId))
                return CreateErrorResponse("experimentId is required");

            // This is where the real power lives.
            // A full implementation would:
            // 1. Load the experiment definition
            // 2. For each trial, set the parameters on the relevant GameObjects / ScriptableObjects
            // 3. Enter play mode (or use a more efficient test runner if available)
            // 4. Execute the defined inputSequences using the existing SimulateInput infrastructure
            // 5. Run playmode.observe on the parameters under test + key feel metrics
            // 6. Capture viewport screenshots at key moments for qualitative review
            // 7. Record everything back into the experiment + feed insights into project memory

            return CreateSuccessResponse(new
            {
                experimentId = args.experimentId,
                trialsScheduled = args.trialCount > 0 ? args.trialCount : 12,
                status = "running",
                note = "In the full implementation this would execute real play sessions using the existing observation + input simulation systems and store rich quantitative + visual results."
            }, "Trial run initiated (skeleton)");
        }

        private object AnalyzeResults(LabParams args)
        {
            // Would correlate trial data against the Living Design Document goals
            // and produce ranked recommendations + statistical significance.

            return CreateSuccessResponse(new
            {
                experimentId = args.experimentId,
                championVariant = "TBD in full implementation",
                confidence = "high",
                recommendation = "Apply the champion using lab.apply_champion after human review."
            }, "Analysis complete (skeleton)");
        }

        private object ApplyChampion(LabParams args)
        {
            return CreateSuccessResponse(new
            {
                applied = false,
                note = "Full implementation will safely apply the winning parameter set back into the project assets with undo support and a memory. recordInsight entry."
            }, "Champion application (skeleton)");
        }
    }
}