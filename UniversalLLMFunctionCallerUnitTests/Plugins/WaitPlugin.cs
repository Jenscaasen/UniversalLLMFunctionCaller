// Copyright (c) Microsoft. All rights reserved.

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace UniversalLLMFunctionCallerDemo.Plugins;

/// <summary>
/// WaitPlugin provides a set of functions to wait before making the rest of operations.
/// </summary>
public sealed class WaitPlugin
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="WaitPlugin"/> class.
    /// </summary>
    public WaitPlugin() : this(null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="WaitPlugin"/> class.
    /// </summary>
    /// <param name="timeProvider">An optional time provider. If not provided, a default time provider will be used.</param>
    [ActivatorUtilitiesConstructor]
    public WaitPlugin(TimeProvider? timeProvider = null) =>
        _timeProvider = timeProvider ?? TimeProvider.System;

}
