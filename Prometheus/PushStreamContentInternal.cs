// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// Simplified for prometheus-net for dependency reduction reasons.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;

namespace Prometheus;

/// <summary>
/// Provides an <see cref="HttpContent"/> implementation that exposes an output <see cref="Stream"/>
/// which can be written to directly. The ability to push data to the output stream differs from the 
/// <see cref="StreamContent"/> where data is pulled and not pushed.
/// </summary>
sealed class PushStreamContentInternal : HttpContent
{
    private readonly Func<Stream, HttpContent, TransportContext?, Task> _onStreamAvailable;

    private static readonly MediaTypeHeaderValue OctetStreamHeaderValue = MediaTypeHeaderValue.Parse("application/octet-stream");

    /// <summary>
    /// Initializes a new instance of the <see cref="PushStreamContentInternal"/> class with the given <see cref="MediaTypeHeaderValue"/>.
    /// </summary>
    public PushStreamContentInternal(Func<Stream, HttpContent, TransportContext?, Task> onStreamAvailable, MediaTypeHeaderValue mediaType)
    {
        _onStreamAvailable = onStreamAvailable;
        Headers.ContentType = mediaType ?? OctetStreamHeaderValue;
    }

    /// <summary>
    /// When this method is called, it calls the action provided in the constructor with the output 
    /// stream to write to. Once the action has completed its work it closes the stream which will 
    /// close this content instance and complete the HTTP request or response.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> to which to write.</param>
    /// <param name="context">The associated <see cref="TransportContext"/>.</param>
    /// <returns>A <see cref="Task"/> instance that is asynchronously serializing the object's content.</returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exception is passed as task result.")]
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        TaskCompletionSource<bool> serializeToStreamTask = new TaskCompletionSource<bool>();

        Stream wrappedStream = new CompleteTaskOnCloseStream(stream, serializeToStreamTask);
        await _onStreamAvailable(wrappedStream, this, context);

        // wait for wrappedStream.Close/Dispose to get called.
        await serializeToStreamTask.Task;
    }

    /// <summary>
    /// Computes the length of the stream if possible.
    /// </summary>
    /// <param name="length">The computed length of the stream.</param>
    /// <returns><c>true</c> if the length has been computed; otherwise <c>false</c>.</returns>
    protected override bool TryComputeLength(out long length)
    {
        // We can't know the length of the content being pushed to the output stream.
        length = -1;
        return false;
    }

    internal class CompleteTaskOnCloseStream : DelegatingStreamInternal
    {
        private TaskCompletionSource<bool> _serializeToStreamTask;

        public CompleteTaskOnCloseStream(Stream innerStream, TaskCompletionSource<bool> serializeToStreamTask)
            : base(innerStream)
        {
            _serializeToStreamTask = serializeToStreamTask;
        }

        [SuppressMessage(
            "Microsoft.Usage",
            "CA2215:Dispose methods should call base class dispose",
            Justification = "See comments, this is intentional.")]
        protected override void Dispose(bool disposing)
        {
            // We don't dispose the underlying stream because we don't own it. Dispose in this case just signifies
            // that the user's action is finished.
            _serializeToStreamTask.TrySetResult(true);
        }
    }
}
