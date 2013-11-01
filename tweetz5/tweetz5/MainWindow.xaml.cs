﻿// Copyright (c) 2013 Blue Onion Software - All rights reserved

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using tweetz5.Controls;
using tweetz5.Model;
using tweetz5.Utilities.System;
using Settings = tweetz5.Properties.Settings;

// ReSharper disable CanBeReplacedWithTryCastAndCheckForNull

namespace tweetz5
{
    public partial class MainWindow
    {
        public static readonly RoutedCommand ReplyCommand = new RoutedUICommand();
        public static readonly RoutedCommand RetweetCommand = new RoutedUICommand();
        public static readonly RoutedCommand RetweetClassicCommand = new RoutedUICommand();
        public static readonly RoutedCommand FavoritesCommand = new RoutedUICommand();
        public static readonly RoutedCommand DeleteTweetCommand = new RoutedUICommand();
        public static readonly RoutedCommand CopyCommand = new RoutedUICommand();
        public static readonly RoutedCommand CopyLinkCommand = new RoutedUICommand();
        public static readonly RoutedCommand OpenTweetLinkCommand = new RoutedUICommand();
        public static readonly RoutedCommand UpdateStatusHomeTimelineCommand = new RoutedUICommand();
        public static readonly RoutedCommand SwitchTimelinesCommand = new RoutedUICommand();
        public static readonly RoutedCommand ShowUserInformationCommand = new RoutedUICommand();
        public static readonly RoutedCommand OpenLinkCommand = new RoutedUICommand();
        public static readonly RoutedCommand FollowUserComand = new RoutedUICommand();
        public static readonly RoutedCommand SearchCommand = new RoutedUICommand();
        public static readonly RoutedCommand AlertCommand = new RoutedUICommand();
        public static readonly RoutedCommand SignOutCommand = new RoutedUICommand();
        public static readonly RoutedCommand SettingsCommand = new RoutedUICommand();
        public static readonly RoutedCommand UpdateLayoutCommand = new RoutedUICommand();
        public static readonly RoutedCommand OpenComposeCommand = new RoutedUICommand();
        public static readonly RoutedCommand ShortcutHelpCommand = new RoutedUICommand();

        public MainWindow()
        {
            InitializeComponent();
            MainPanel.IsVisibleChanged += MainPanelOnIsVisibleChanged;
            Loaded += (sender, args) =>
            {
                // HACK: Compose.Toggle does not work the first time unless the control is initially visible.
                Compose.Visibility = Visibility.Collapsed;
                SignIn();
            };
        }

        public void SignIn()
        {
            var buildDate = BuildInfo.GetBuildDateTime();
            if (DateTime.Now > buildDate.AddMonths(3))
            {
                Settings.Default.AccessToken = string.Empty;
                Settings.Default.AccessTokenSecret = string.Empty;
                AlertCommand.Execute("Expired", this);
                return;
            }
            SettingsPanel.Visibility = Visibility.Collapsed;
            if (string.IsNullOrWhiteSpace(Settings.Default.UserId))
            {
                AuthenticatePanel.Visibility = Visibility.Visible;
                MainPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                AuthenticatePanel.Visibility = Visibility.Collapsed;
                Timeline.Controller.StartTimelines();
            }
        }

        private void DragMoveWindow(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void BottomSizeOnDragDelta(object sender, DragDeltaEventArgs e)
        {
            Height = Math.Max(Height + e.VerticalChange, MinHeight);
        }

        private void RightSizeBarOnDragDelta(object sender, DragDeltaEventArgs e)
        {
            Width = Math.Max(Width + e.HorizontalChange, MinWidth);
        }

        private void MainPanelOnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (MainPanel.IsVisible)
            {
                ResizeBar.Visibility = Visibility.Visible;
                Timeline.Visibility = Visibility.Visible;
                OnRenderSizeChanged(new SizeChangedInfo(this, new Size(Width, Height), true, true));
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Timeline.Height = e.NewSize.Height - Topbar.ActualHeight - NavBar.ActualHeight - Compose.ActualHeight - ResizeBar.ActualHeight;
            Timeline.Width = Math.Max(0, e.NewSize.Width - RightSizeBar.ActualWidth - 2);

            SettingsPanel.Height = Timeline.Height;
            SettingsPanel.Width = Timeline.Width;

            AuthenticatePanel.Width = Timeline.Width;
            Compose.Width = Timeline.Width;
        }

        private void ComposeOnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            OnRenderSizeChanged(new SizeChangedInfo(this, new Size(Width, Height), true, true));
            if (Compose.IsVisible) Compose.Focus();
        }

        private void SwitchTimeslinesCommandHandler(object sender, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            var timelineName = (string)ea.Parameter;
            Compose.Visibility = Visibility.Collapsed;
            SettingsPanel.Visibility = Visibility.Collapsed;
            ResizeBar.Visibility = Visibility.Visible;
            Timeline.Visibility = Visibility.Visible;
            MainPanel.Visibility = Visibility.Visible;
            SetButtonStates(timelineName);
            Timeline.Controller.SwitchTimeline(timelineName);
            Timeline.ScrollToTop();
            Timeline.Focus();
        }

        private void SetButtonStates(string timelineName)
        {
            UnifiedButton.IsEnabled = timelineName != Timelines.UnifiedName;
            HomeButton.IsEnabled = timelineName != Timelines.HomeName;
            MentionsButton.IsEnabled = timelineName != Timelines.MentionsName;
            MessagesButton.IsEnabled = timelineName != Timelines.MessagesName;
            FavoritesButton.IsEnabled = timelineName != Timelines.FavoritesName;
            SearchButton.IsEnabled = timelineName != Timelines.SearchName;
            SettingsButton.IsEnabled = timelineName != "settings";
        }

        private void CopyCommandHandler(object target, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            var tweet = ea.Parameter as Tweet ?? Timeline.GetSelectedTweet;
            if (tweet == null) return;
            Timeline.Controller.CopyTweetToClipboard(tweet);
        }

        private void CopyLinkCommandHandler(object target, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            var tweet = ea.Parameter as Tweet ?? Timeline.GetSelectedTweet;
            if (tweet == null) return;
            Timeline.Controller.CopyLinkToClipboard(tweet);
        }

        private void OpenTweetLinkCommandHandler(object target, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            var tweet = (Tweet)ea.Parameter ?? Timeline.GetSelectedTweet;
            var link = TimelineController.TweetLink(tweet);
            OpenLinkCommand.Execute(link, this);
        }

        private void ReplyCommandHandler(object sender, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            var tweet = ea.Parameter as Tweet ?? Timeline.GetSelectedTweet;
            if (tweet == null) return;
            if (tweet.IsDirectMesssage)
            {
                Compose.ShowDirectMessage(tweet.Name, tweet.ScreenName);
            }
            else
            {
                var matches = new Regex(@"(?<=^|(?<=[^a-zA-Z0-9-_\.]))@([A-Za-z]+[A-Za-z0-9]+)")
                    .Matches(tweet.Text);

                var names = matches
                    .Cast<Match>()
                    .Where(m => m.Groups[0].Value != tweet.ScreenName)
                    .Select(m => m.Groups[0].Value)
                    .Distinct();

                var replyTos = string.Join(" ", names);
                var message = string.Format("@{0} {1}{2}", tweet.ScreenName, replyTos, (replyTos.Length > 0) ? " " : "");
                Compose.Show(message, tweet.StatusId);
            }
        }

        private void RetweetCommandHandler(object sender, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            var tweet = ea.Parameter as Tweet ?? Timeline.GetSelectedTweet;
            if (tweet == null) return;
            Timeline.Controller.Retweet(tweet);
        }

        private void RetweetClassicCommandHandler(object sender, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            var tweet = (Tweet)ea.Parameter ?? Timeline.GetSelectedTweet;
            var message = string.Format("RT @{0} {1}", tweet.ScreenName, tweet.Text);
            Compose.Show(message);
        }

        private void FavoritesCommandHandler(object sender, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            var tweet = ea.Parameter as Tweet ?? Timeline.GetSelectedTweet;
            if (tweet == null) return;
            if (tweet.Favorited) Timeline.Controller.RemoveFavorite(tweet);
            else Timeline.Controller.AddFavorite(tweet);
        }

        private void UpdateStatusHomeTimelineHandler(object sender, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            var statuses = (Status[])ea.Parameter;
            Timeline.Controller.UpdateStatus(new[] { Timelines.HomeName, Timelines.UnifiedName }, statuses, "h");
        }

        private void CloseCommandHandler(object sender, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            Close();
        }

        private void ShowUserInformationCommandHandler(object sender, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            UserInformationPopup.ScreenName = ea.Parameter as string;
            UserInformationPopup.IsOpen = true;
        }

        private void OpenLinkCommandHandler(object sender, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            Process.Start(new ProcessStartInfo((string)ea.Parameter));
        }

        private void FollowCommandHandler(object sender, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            var user = (User)ea.Parameter;
            user.Following = user.Following ? !Twitter.Unfollow(user.ScreenName) : Twitter.Follow(user.ScreenName);
        }

        private void NotifyCommandHandler(object sender, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            if (Settings.Default.Chirp == false) return;
            var player = new SoundPlayer { Stream = Properties.Resources.Notify };
            player.Play();
        }

        private void DeleteTweetCommandHander(object sender, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            var tweet = ea.Parameter as Tweet;
            if (tweet == null) return;
            Timeline.Controller.DeleteTweet(tweet);
        }

        private void SearchCommandHandler(object sender, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            var query = ea.Parameter as string;
            if (string.IsNullOrWhiteSpace(query)) return;
            Timeline.SearchControl.SetSearchText(query);
            SwitchTimelinesCommand.Execute(Timelines.SearchName, this);
            Timeline.Controller.Search(query);
        }

        private void AlertCommandHandler(object sender, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            var message = ea.Parameter as string;
            if (string.IsNullOrWhiteSpace(message)) return;
            StatusAlert.Message.Text = message;
            StatusAlert.IsOpen = true;
        }

        private void ShortcutHelpCommandHandler(object sender, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            ShortcutHelp.IsOpen = true;
        }

        private void SignOutCommandHandler(object sender, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            Timeline.Controller.StopTimelines();
            Settings.Default.AccessToken = "";
            Settings.Default.AccessTokenSecret = "";
            Settings.Default.ScreenName = "";
            Settings.Default.UserId = "";
            Settings.Default.Save();
            SignIn();
        }

        private void SettingsCommandHandler(object sender, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            SettingsPanel.Visibility = Visibility.Visible;
            Timeline.Visibility = Visibility.Collapsed;
            SetButtonStates("settings");
        }

        private void OpenComposeCommandHandler(object sender, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            Compose.Toggle();
        }

        private void UpdateLayoutCommandHandler(object sender, ExecutedRoutedEventArgs ea)
        {
            ea.Handled = true;
            OnRenderSizeChanged(new SizeChangedInfo(this, new Size(Width, Height), true, true));
        }

        private void OnDragOver(object sender, DragEventArgs ea)
        {
            ea.Handled = true;
            ea.Effects = DragDropEffects.None;
            if (ea.Data.GetDataPresent("text/html"))
            {
                ea.Effects = DragDropEffects.Copy;
            }
            else if (ea.Data.GetDataPresent(DataFormats.FileDrop, true))
            {
                var filenames = ea.Data.GetData(DataFormats.FileDrop, true) as string[];
                if (filenames != null && filenames.Length == 1 && IsValidImageExtension(filenames[0]))
                {
                    ea.Effects = DragDropEffects.Copy;
                }
            }
        }

        private void OnDrop(object sender, DragEventArgs ea)
        {
            if (ea.Data.GetDataPresent("text/html"))
            {
                var html = string.Empty;
                var data = ea.Data.GetData("text/html");
                if (data is string)
                {
                    html = (string)data;
                }
                else if (data is MemoryStream)
                {
                    var stream = (MemoryStream)data;
                    var buffer = new byte[stream.Length];
                    stream.Read(buffer, 0, buffer.Length);
                    html = (buffer[1] == (byte)0)
                        ? Encoding.Unicode.GetString(buffer)
                        : Encoding.ASCII.GetString(buffer);
                }
                var match = new Regex(@"<img[^>]+src=""([^""]*)""").Match(html);
                if (match.Success)
                {
                    var uri = new Uri(match.Groups[1].Value);
                    var filename = Path.GetTempFileName();
                    var webClient = new WebClient();
                    try
                    {
                        webClient.DownloadFileCompleted += (o, args) =>
                        {
                            Compose.Visibility = Visibility.Visible;
                            Compose.Image = filename;
                            webClient.Dispose();
                        };
                        webClient.DownloadFileAsync(uri, filename);
                        ea.Handled = true;
                    }
                    catch (WebException)
                    {
                    }
                }
            }
            else if (ea.Data.GetDataPresent(DataFormats.FileDrop, true))
            {
                var filenames = ea.Data.GetData(DataFormats.FileDrop, true) as string[];
                if (filenames != null && filenames.Length == 1 && IsValidImageExtension(filenames[0]))
                {
                    Compose.Visibility = Visibility.Visible;
                    Compose.Image = filenames[0];
                    ea.Handled = true;
                }
            }
        }

        private static bool IsValidImageExtension(string filename)
        {
            var extension = Path.GetExtension(filename);
            var extensions = new[] { ".png", ".jpg", ".jpeg", ".gif" };
            return extensions.Any(e => extension.Equals(e, StringComparison.OrdinalIgnoreCase));
        }
    }
}