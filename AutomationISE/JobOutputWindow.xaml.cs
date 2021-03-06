﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using System.Timers;
using Microsoft.Azure.Management.Automation.Models;
using AutomationISE.Model;

namespace AutomationISE
{
    /// <summary>
    /// Interaction logic for TestJobOutputWindow.xaml
    /// </summary>
    public partial class JobOutputWindow : Window
    {
        private JobCreateResponse jobCreateResponse = null;
        private AutomationISEClient iseClient;
        private String runbookName;
        private Timer refreshTimer;
        /* These values are the defaults for the settings visible using >(Get-Host).PrivateData */
        public static String ErrorForegroundColorCode = "#FFFF0000";
        public static String ErrorBackgroundColorCode = "#00FFFFFF";
        public static String WarningForegroundColorCode = "#FFFF8C00";
        public static String WarningBackgroundColorCode = "#00FFFFFF";
        public static String VerboseForegroundColorCode = "#FF00FFFF";
        public static String VerboseBackgroundColorCode = "#00FFFFFF";

        public JobOutputWindow(String name, AutomationISEClient client)
        {
            InitializeComponent();
            this.Title = name + " Test Job";
            AdditionalInformation.Text = "Tip: not seeing Verbose output? Add the line \"$VerbosePreference='Continue'\" to your runbook.";
            runbookName = name;
            iseClient = client;
            Task t = checkTestJob();
            refreshTimer = new Timer();
            refreshTimer.Interval = 30000;
            refreshTimer.Elapsed += new ElapsedEventHandler(refresh);
        }

        //TODO: refactor this to a different class with some inheritance structure
        public JobOutputWindow(String name, JobCreateResponse response, AutomationISEClient client)
        {
            InitializeComponent();
            StartJobButton.IsEnabled = false;
            StopJobButton.IsEnabled = false;
            this.Title = "Job: " + name;
            AdditionalInformation.Text = "This is a Global Runbook responsible for syncing your GitHub repo with your Automation Account. Neato!";
            runbookName = name;
            jobCreateResponse = response;
            iseClient = client;
            Task t = checkJob();
            refreshTimer = new Timer();
            refreshTimer.Interval = 30000;
            refreshTimer.Elapsed += new ElapsedEventHandler(refresh);
        }

        private async Task checkTestJob()
        {
            TestJobGetResponse response = await iseClient.automationManagementClient.TestJobs.GetAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                                                iseClient.currAccount.Name, runbookName, new System.Threading.CancellationToken());
            JobDetails.Content = runbookName + " test job created at " + response.TestJob.CreationTime.LocalDateTime;
            JobDetails.Content += "\r\nLast refreshed at " + DateTime.Now;
            JobStatus.Content = response.TestJob.Status;
            if (response.TestJob.Status == "Failed")
            {
                updateJobOutputTextBlockWithException(response.TestJob.Exception);
                refreshTimer.Stop();
            }
            else
            {
                JobStreamListResponse jslResponse = await iseClient.automationManagementClient.JobStreams.ListTestJobStreamsAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                    iseClient.currAccount.Name, runbookName, null, new System.Threading.CancellationToken());
                /* Write out each stream's output */
                foreach (JobStream stream in jslResponse.JobStreams)
                {
                    var jslStream = await iseClient.automationManagementClient.JobStreams.GetTestJobStreamAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                            iseClient.currAccount.Name, runbookName, stream.Properties.JobStreamId, new System.Threading.CancellationToken());
                    updateJobOutputTextBlock(jslStream);
                }
                if (response.TestJob.Status == "Suspended")
                {
                    updateJobOutputTextBlockWithException(response.TestJob.Exception);
                    refreshTimer.Stop();
                }
                else if (response.TestJob.Status == "Completed")
                    refreshTimer.Stop();
            }
        }

        private async Task checkJob()
        {
            JobGetResponse response = await iseClient.automationManagementClient.Jobs.GetAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                                                iseClient.currAccount.Name, jobCreateResponse.Job.Properties.JobId, new System.Threading.CancellationToken());

            JobDetails.Content = runbookName + " test job created at " + response.Job.Properties.CreationTime.LocalDateTime;
            JobDetails.Content += "\r\nLast refreshed at " + DateTime.Now;
            JobStatus.Content = response.Job.Properties.Status;

            JobStreamListResponse jslResponse = await iseClient.automationManagementClient.JobStreams.ListAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                iseClient.currAccount.Name, jobCreateResponse.Job.Properties.JobId, null, new System.Threading.CancellationToken());

            foreach (JobStream stream in jslResponse.JobStreams)
            {
                var jslStream = await iseClient.automationManagementClient.JobStreams.GetAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                        iseClient.currAccount.Name, jobCreateResponse.Job.Properties.JobId, stream.Properties.JobStreamId, new System.Threading.CancellationToken());
                updateJobOutputTextBlock(jslStream);
            }
        }

        private void updateJobOutputTextBlock(JobStreamGetResponse stream)
        {
            String streamText = stream.JobStream.Properties.StreamText;
            OutputTextBlockParagraph.Inlines.Add("\r\n");
            if (stream.JobStream.Properties.StreamType == "Output")
            {
                OutputTextBlockParagraph.Inlines.Add(streamText);
            }
            else if (stream.JobStream.Properties.StreamType == "Verbose")
            {
                streamText = "VERBOSE: " + streamText;
                OutputTextBlockParagraph.Inlines.Add(new Run(streamText)
                {
                    Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom(VerboseForegroundColorCode)),
                    Background = (SolidColorBrush)(new BrushConverter().ConvertFrom(VerboseBackgroundColorCode))
                });
            }
            else if (stream.JobStream.Properties.StreamType == "Error")
            {
                streamText = "ERROR: " + streamText;
                OutputTextBlockParagraph.Inlines.Add(new Run(streamText)
                {
                    Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom(ErrorForegroundColorCode)),
                    Background = (SolidColorBrush)(new BrushConverter().ConvertFrom(ErrorBackgroundColorCode))
                });
            }
            else if (stream.JobStream.Properties.StreamType == "Warning")
            {
                streamText = "WARNING: " + streamText;
                OutputTextBlockParagraph.Inlines.Add(new Run(streamText)
                {
                    Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom(WarningForegroundColorCode)),
                    Background = (SolidColorBrush)(new BrushConverter().ConvertFrom(WarningBackgroundColorCode))
                });
            }
            else
            {
                Debug.WriteLine("Unknown stream type couldn't be colored properly: " + stream.JobStream.Properties.StreamType);
                OutputTextBlockParagraph.Inlines.Add(stream.JobStream.Properties.StreamType.ToUpper() + ":  " + streamText);
            }
        }

        private void updateJobOutputTextBlockWithException(string exceptionMessage)
        {
            OutputTextBlockParagraph.Inlines.Add("\r\n");
            OutputTextBlockParagraph.Inlines.Add(new Run(exceptionMessage)
            {
                Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom(ErrorForegroundColorCode)),
                Background = (SolidColorBrush)(new BrushConverter().ConvertFrom(ErrorBackgroundColorCode))
            });
        }


        private void refresh(object source, ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                OutputTextBlockParagraph.Inlines.Clear();
                Task t;
                if (jobCreateResponse != null)
                    t = checkJob();
                else 
                    t = checkTestJob();
            });
        }

        private async void RefreshJobButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshJobButton.IsEnabled = false;
                RefreshJobButton.Content = "Refreshing...";
                refreshTimer.Stop();
                OutputTextBlockParagraph.Inlines.Clear();
                if (jobCreateResponse != null) await checkJob();
                else await checkTestJob();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Refresh Failure", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                refreshTimer.Start();
                RefreshJobButton.IsEnabled = true;
                RefreshJobButton.Content = "Refresh";
            }
        }

        private async void StopJobButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StopJobButton.IsEnabled = false;
                StopJobButton.Content = "Stopping...";
                Microsoft.Azure.AzureOperationResponse response = await iseClient.automationManagementClient.TestJobs.StopAsync(
                    iseClient.accountResourceGroups[iseClient.currAccount].Name,
                    iseClient.currAccount.Name, runbookName, new System.Threading.CancellationToken());
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    throw new Exception("The job couldn't be stopped.\r\nReceived status code: " + response.StatusCode);
                JobStatus.Content = "Submitted job stop request";
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Job Stop Failure", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                refreshTimer.Start();
                StopJobButton.IsEnabled = true;
                StopJobButton.Content = "Stop Job";
            }
        }

        private async Task<IDictionary<string, string>> GetTestJobParams()
        {
            try {
                TestJobGetResponse response = await iseClient.automationManagementClient.TestJobs.GetAsync(iseClient.accountResourceGroups[iseClient.currAccount].Name,
                                        iseClient.currAccount.Name, runbookName, new System.Threading.CancellationToken());
                IDictionary<string, string> jobParams = response.TestJob.Parameters;
                return jobParams;
            }
            catch
            {
                // return null if test job not found.
                return null;
            }
        }

        private async void StartJobButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StartJobButton.IsEnabled = false;
                refreshTimer.Stop();
                TestJobCreateResponse response = await createTestJob();
                if (response != null)
                {
                    OutputTextBlockParagraph.Inlines.Clear();
                    JobDetails.Content = runbookName + " test job created at " + response.TestJob.CreationTime.LocalDateTime;
                    JobStatus.Content = response.TestJob.Status;
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Job Start Failure", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                refreshTimer.Start();
                StartJobButton.IsEnabled = true;
            }
        }
        private async Task<TestJobCreateResponse> createTestJob()
        {
            RunbookDraft draft = await AutomationRunbookManager.GetRunbookDraft(runbookName, iseClient.automationManagementClient,
                            iseClient.accountResourceGroups[iseClient.currAccount].Name, iseClient.currAccount.Name);
            if (draft.InEdit == false)
                throw new Exception("This runbook has no draft to test because it is in a 'Published' state.");
            TestJobCreateParameters jobCreationParams = new TestJobCreateParameters();
            jobCreationParams.RunbookName = runbookName;
            if (draft.Parameters.Count > 0)
            {
                /* User needs to specify values for them */
                var existingParams = await GetTestJobParams();
                RunbookParamDialog paramDialog = new RunbookParamDialog(draft.Parameters,existingParams);
                if (paramDialog.ShowDialog() == true)
                    jobCreationParams.Parameters = paramDialog.paramValues;
                else
                    return null;
            }
            /* start the test job */
            TestJobCreateResponse jobResponse = await iseClient.automationManagementClient.TestJobs.CreateAsync(
                            iseClient.accountResourceGroups[iseClient.currAccount].Name,
                            iseClient.currAccount.Name, jobCreationParams, new System.Threading.CancellationToken());
            if (jobResponse == null || jobResponse.StatusCode != System.Net.HttpStatusCode.Created)
                throw new Exception("The test job could not be created: received HTTP status code " + jobResponse.StatusCode);
            return jobResponse;
        }
    }
}
