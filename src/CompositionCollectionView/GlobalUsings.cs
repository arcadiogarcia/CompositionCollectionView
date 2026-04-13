// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.ComponentModel;
global using System.Numerics;
global using Microsoft.Toolkit.Uwp.UI.Animations.ExpressionsFork;

#if !WINAPPSDK
global using Windows.UI;
global using Windows.UI.Composition;
global using Windows.UI.Composition.Interactions;
global using Windows.UI.Xaml;
global using Windows.UI.Xaml.Controls;
global using Windows.UI.Xaml.Hosting;
global using Windows.UI.Xaml.Media;
#else
global using Microsoft.UI;
global using Microsoft.UI.Composition;
global using Microsoft.UI.Composition.Interactions;
global using Microsoft.UI.Xaml;
global using Microsoft.UI.Xaml.Controls;
global using Microsoft.UI.Xaml.Hosting;
global using Microsoft.UI.Xaml.Media;
#endif
