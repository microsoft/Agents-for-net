// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Core.Reactive
{
    /// <summary>
    /// Bridging utilities for IAsyncEnumerable and IObservable interop.
    /// </summary>
    /// <remarks>
    /// These utilities are provided for migration purposes. The long-term goal
    /// is to standardize on IObservable for push-based event streams. New code should
    /// prefer IObservable directly.
    /// </remarks>
    public static class ObservableBridge
    {
        /// <summary>
        /// Converts an IAsyncEnumerable to IObservable.
        /// </summary>
        /// <typeparam name="T">The type of elements in the sequence.</typeparam>
        /// <param name="source">The async enumerable source.</param>
        /// <returns>An observable sequence containing the same elements.</returns>
        /// <remarks>
        /// Consider using IObservable directly for new code.
        /// </remarks>
        public static IObservable<T> ToObservable<T>(this IAsyncEnumerable<T> source)
        {
            AssertionHelpers.ThrowIfNull(source, nameof(source));

            return Observable.Create<T>(async (observer, cancellationToken) =>
            {
                try
                {
                    await foreach (var item in source.ConfigureAwait(false))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                        observer.OnNext(item);
                    }
                    observer.OnCompleted();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Expected cancellation - just complete
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                }
            });
        }

        /// <summary>
        /// Converts an IObservable to IAsyncEnumerable.
        /// </summary>
        /// <typeparam name="T">The type of elements in the sequence.</typeparam>
        /// <param name="source">The observable source.</param>
        /// <returns>An async enumerable sequence containing the same elements.</returns>
        /// <remarks>
        /// Prefer keeping data as IObservable when possible.
        /// </remarks>
        public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IObservable<T> source)
        {
            AssertionHelpers.ThrowIfNull(source, nameof(source));

            return source.ToAsyncEnumerable();
        }
    }
}
