// <copyright file="DefaultSslStreamWrapperFactory.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using System;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

using Microsoft.Extensions.Logging;

namespace FubarDev.FtpServer.Authentication
{
    /// <summary>
    /// The default implementation of the <see cref="ISslStreamWrapperFactory"/> interface.
    /// </summary>
    public class DefaultSslStreamWrapperFactory : ISslStreamWrapperFactory
    {
        [CanBeNull]
        private readonly ILogger<ISslStreamWrapperFactory> _logger;

        public DefaultSslStreamWrapperFactory(
            [CanBeNull] ILogger<ISslStreamWrapperFactory> logger = null)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<Stream> WrapStreamAsync(
            Stream unencryptedStream,
            bool keepOpen,
            X509Certificate certificate,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger?.LogTrace("Create SSL stream");
                var sslStream = CreateSslStream(unencryptedStream, keepOpen);
                try
                {
                    var certCopy = new X509Certificate2(certificate.Export(X509ContentType.Pkcs12));
                    _logger?.LogTrace("Authenticate as server");
                    await sslStream.AuthenticateAsServerAsync(certCopy)
                       .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(0, ex, ex.Message);
                    sslStream.Dispose();
                    throw;
                }

                return sslStream;
            }
            catch (Exception ex)
            {
                _logger?.LogError(0, ex, ex.Message);
                throw;
            }
        }

#if NETCOREAPP || NET47
        /// <inheritdoc />
        public async Task CloseStreamAsync(Stream sslStream, CancellationToken cancellationToken)
        {
            if (sslStream is SslStream s)
            {
                await s.ShutdownAsync().ConfigureAwait(false);

                // Why is this needed? I get a GnuTLS error -110 when it's not called!
                await Task.Yield();

                await s.FlushAsync(cancellationToken).ConfigureAwait(false);
                s.Close();
            }
        }
#else
        /// <inheritdoc />
        public Task CloseStreamAsync(Stream sslStream, CancellationToken cancellationToken)
        {
            if (sslStream is SslStream s)
            {
#if NET461 || NETSTANDARD2_0
                s.Close();
#else
                s.Dispose();
#endif
            }

            return Task.CompletedTask;
        }
#endif

        /// <summary>
        /// Create a new <see cref="SslStream"/> instance.
        /// </summary>
        /// <param name="unencryptedStream">The stream to wrap in an <see cref="SslStream"/> instance.</param>
        /// <param name="keepOpen">Keep the inner stream open.</param>
        /// <returns>The new <see cref="SslStream"/>.</returns>
        protected virtual SslStream CreateSslStream(
            Stream unencryptedStream,
            bool keepOpen)
        {
#if USE_GNU_SSL_STREAM
            return new GnuSslStream(unencryptedStream, keepOpen);
#else
            return new SslStream(unencryptedStream, keepOpen);
#endif
        }
    }
}
