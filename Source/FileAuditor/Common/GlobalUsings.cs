global using System;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.ComponentModel;
global using System.Data;
global using System.Diagnostics;
global using System.Globalization;
global using System.IO;
global using System.Linq;
global using System.Reflection;
global using System.Text;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Threading;
global using System.Threading.Channels;
global using System.Threading.Tasks;

global using Avalonia;
global using Avalonia.Controls;
global using Avalonia.Controls.ApplicationLifetimes;
global using Avalonia.Interactivity;
global using Avalonia.Markup.Xaml;
global using Avalonia.Platform.Storage;
global using Avalonia.Threading;
global using Avalonia.VisualTree;

global using Classic.Avalonia.Theme;

global using CommunityToolkit.Mvvm.ComponentModel;
global using CommunityToolkit.Mvvm.Input;

global using ErrorOr;

global using Microsoft.Data.SqlClient;

global using FileAuditor.Common.Errors;
global using FileAuditor.Common.Extensions;
global using FileAuditor.Common.Serialisation;
global using FileAuditor.Common.Text;
global using FileAuditor.Models.Auditing;
global using FileAuditor.Models.Reporting;
global using FileAuditor.Models.Settings;
global using FileAuditor.Services.Auditing;
global using FileAuditor.Services.Checking;
global using FileAuditor.Services.Dialogues;
global using FileAuditor.Services.Reporting;
global using FileAuditor.Services.Settings;
global using FileAuditor.Services.Sql;
global using FileAuditor.ViewModels.About;
global using FileAuditor.ViewModels.Dialogues;
global using FileAuditor.ViewModels.Groups;
global using FileAuditor.ViewModels.Main;
global using FileAuditor.ViewModels.Tabs;
global using FileAuditor.Views.About;
global using FileAuditor.Views.Dialogues;
global using FileAuditor.Views.Main;
global using FileAuditor.Views.Tabs;
