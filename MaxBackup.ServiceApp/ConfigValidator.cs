using System.Text.Json;
using MaxBackup.Shared;

namespace MaxBackup.ServiceApp;

public static class ConfigValidator
{
    public static List<ValidationError> ValidateConfig(string configPath)
    {
        var errors = new List<ValidationError>();

        // Check if file exists
        if (!File.Exists(configPath))
        {
            errors.Add(new ValidationError
            {
                Field = "ConfigFile",
                Error = $"Config file not found at {configPath}"
            });
            return errors;
        }

        try
        {
            // Try to parse JSON
            var json = File.ReadAllText(configPath);
            var doc = JsonDocument.Parse(json);

            // Check for Backup section
            if (!doc.RootElement.TryGetProperty("Backup", out var backupSection))
            {
                errors.Add(new ValidationError
                {
                    Field = "Backup",
                    Error = "Config must have a 'Backup' section"
                });
                return errors;
            }

            // Check for Jobs array
            if (!backupSection.TryGetProperty("Jobs", out var jobsElement))
            {
                errors.Add(new ValidationError
                {
                    Field = "Backup.Jobs",
                    Error = "'Backup' section must have a 'Jobs' array"
                });
                return errors;
            }

            if (jobsElement.ValueKind != JsonValueKind.Array)
            {
                errors.Add(new ValidationError
                {
                    Field = "Backup.Jobs",
                    Error = "'Jobs' must be an array"
                });
                return errors;
            }

            var jobs = jobsElement.EnumerateArray().ToList();
            if (jobs.Count == 0)
            {
                errors.Add(new ValidationError
                {
                    Field = "Backup.Jobs",
                    Error = "'Jobs' array cannot be empty. Add at least one backup job."
                });
                return errors;
            }

            // Validate each job
            int jobIndex = 0;
            foreach (var job in jobs)
            {
                ValidateJob(job, jobIndex, errors);
                jobIndex++;
            }
        }
        catch (JsonException ex)
        {
            errors.Add(new ValidationError
            {
                Field = "JSON",
                Error = $"Invalid JSON format: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            errors.Add(new ValidationError
            {
                Field = "ConfigFile",
                Error = $"Error reading config: {ex.Message}"
            });
        }

        return errors;
    }

    private static void ValidateJob(JsonElement job, int jobIndex, List<ValidationError> errors)
    {
        string? jobName = null;

        // Check Name
        if (!job.TryGetProperty("Name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
        {
            errors.Add(new ValidationError
            {
                Job = $"Job[{jobIndex}]",
                Field = "Name",
                Error = "Job must have a 'Name' field (string)"
            });
        }
        else
        {
            jobName = nameElement.GetString();
            if (string.IsNullOrWhiteSpace(jobName))
            {
                errors.Add(new ValidationError
                {
                    Job = $"Job[{jobIndex}]",
                    Field = "Name",
                    Error = "'Name' cannot be empty"
                });
            }
        }

        var jobPrefix = jobName ?? $"Job[{jobIndex}]";

        // Check Source
        if (!job.TryGetProperty("Source", out var sourceElement) || sourceElement.ValueKind != JsonValueKind.String)
        {
            errors.Add(new ValidationError
            {
                Job = jobPrefix,
                Field = "Source",
                Error = "Job must have a 'Source' field (string)"
            });
        }
        else if (string.IsNullOrWhiteSpace(sourceElement.GetString()))
        {
            errors.Add(new ValidationError
            {
                Job = jobPrefix,
                Field = "Source",
                Error = "'Source' cannot be empty"
            });
        }

        // Check Destination
        if (!job.TryGetProperty("Destination", out var destElement) || destElement.ValueKind != JsonValueKind.String)
        {
            errors.Add(new ValidationError
            {
                Job = jobPrefix,
                Field = "Destination",
                Error = "Job must have a 'Destination' field (string)"
            });
        }
        else if (string.IsNullOrWhiteSpace(destElement.GetString()))
        {
            errors.Add(new ValidationError
            {
                Job = jobPrefix,
                Field = "Destination",
                Error = "'Destination' cannot be empty"
            });
        }

        // Check Include
        if (!job.TryGetProperty("Include", out var includeElement) || includeElement.ValueKind != JsonValueKind.Array)
        {
            errors.Add(new ValidationError
            {
                Job = jobPrefix,
                Field = "Include",
                Error = "Job must have an 'Include' field (array of patterns)"
            });
        }

        // Check Exclude
        if (!job.TryGetProperty("Exclude", out var excludeElement) || excludeElement.ValueKind != JsonValueKind.Array)
        {
            errors.Add(new ValidationError
            {
                Job = jobPrefix,
                Field = "Exclude",
                Error = "Job must have an 'Exclude' field (array of patterns)"
            });
        }
    }
}
