﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Steam_Library_Manager.Framework
{
    class TaskManager
    {
        public static BlockingCollection<Definitions.List.TaskList> TaskList = new BlockingCollection<Definitions.List.TaskList>();
        public static CancellationTokenSource CancellationToken;
        public static bool Status = false;
        public static bool IsRestartRequired = false;

        public static void ProcessTask(Definitions.List.TaskList currentTask)
        {
            try
            {
                currentTask.Moving = true;
                currentTask.TargetGame.CopyGameFiles(currentTask, CancellationToken.Token);

                if (!CancellationToken.IsCancellationRequested)
                {
                    // If game is not exists in the target library
                    if (currentTask.TargetLibrary.Games.Count(x => x.AcfName == currentTask.TargetGame.AcfName && currentTask.Compress == x.IsCompressed) == 0)
                    {
                        // Add game to new library
                        Functions.Games.AddNewGame(currentTask.TargetGame.AppID, currentTask.TargetGame.AppName, currentTask.TargetGame.InstallationPath.Name, currentTask.TargetLibrary, currentTask.TargetGame.SizeOnDisk, currentTask.Compress);

                        // Update library details
                        currentTask.TargetLibrary.UpdateLibraryVisual();
                    }

                    if (currentTask.RemoveOldFiles)
                    {
                        if (currentTask.TargetGame.DeleteFiles())
                        {
                            currentTask.TargetGame.RemoveFromLibrary();
                        }
                    }

                    if (!currentTask.TargetLibrary.IsBackup)
                        IsRestartRequired = true;

                    currentTask.Moving = false;
                    currentTask.Completed = true;

                    if (TaskList.Count == 0)
                    {
                        if (Properties.Settings.Default.PlayASoundOnCompletion)
                        {
                            if (!string.IsNullOrEmpty(Properties.Settings.Default.CustomSoundFile) && File.Exists(Properties.Settings.Default.CustomSoundFile))
                                new System.Media.SoundPlayer(Properties.Settings.Default.CustomSoundFile).Play();
                            else
                                System.Media.SystemSounds.Exclamation.Play();
                        }

                        if (IsRestartRequired)
                            Functions.Steam.RestartSteamAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(ex.ToString());
                Functions.Logger.LogToFile(Functions.Logger.LogType.TaskManager, $"[{currentTask.TargetGame.AppName}][{currentTask.TargetGame.AppID}][{currentTask.TargetGame.AcfName}] {ex}");
            }
        }

        public static void Start()
        {
            if (!Status)
            {
                Main.Accessor.TaskManager_Logs.Add($"[{DateTime.Now}][TaskManager] Task Manager is now active and waiting for tasks...");
                Main.Accessor.Button_StartTaskManager.IsEnabled = false;
                Main.Accessor.Button_StopTaskManager.IsEnabled = true;
                CancellationToken = new CancellationTokenSource();
                Status = true;

                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        while (true && !CancellationToken.IsCancellationRequested && Status)
                        {
                            ProcessTask(TaskList.Take(CancellationToken.Token));
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Stop();
                        Main.Accessor.TaskManager_Logs.Add($"[{DateTime.Now}][TaskManager] Task Manager is stopped now...");
                        Functions.Logger.LogToFile(Functions.Logger.LogType.TaskManager, "Task Manager is stopped now");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        MessageBox.Show(ex.ToString());

                        Functions.Logger.LogToFile(Functions.Logger.LogType.TaskManager, ex.ToString());
                    }
                });
            }
        }

        public static void Stop()
        {
            try
            {
                if (Status)
                {
                    Main.Accessor.Button_StartTaskManager.IsEnabled = true;
                    Main.Accessor.Button_StopTaskManager.IsEnabled = false;

                    Status = false;
                    CancellationToken.Cancel();
                    IsRestartRequired = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(ex.ToString());

                Functions.Logger.LogToFile(Functions.Logger.LogType.TaskManager, ex.ToString());
            }
        }

        public static void RemoveTask(Definitions.List.TaskList Task)
        {
            try
            {
                TaskList.TryTake(out Task);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(ex.ToString());
                Functions.Logger.LogToFile(Functions.Logger.LogType.TaskManager, ex.ToString());
            }
        }

    }
}