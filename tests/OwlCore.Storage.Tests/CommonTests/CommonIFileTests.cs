using OwlCore.Extensions;

namespace OwlCore.Storage.Tests.SystemIO
{
    public abstract class CommonIFileTests
    {
        /// <summary>
        /// Call the constructor using valid input parameters.
        /// </summary>
        public abstract Task<IFile> CreateInstance_ValidParameters();

        /// <summary>
        /// Call the constructor using invalid input parameters, such as a wrong path.
        /// </summary>
        public abstract Task<IFile> CreateInstance_InvalidParameters();
        
        [TestMethod]
        public virtual Task ConstructorCall_ValidParameters()
        {
            // Shouldn't throw when constructor is called.
            return CreateInstance_ValidParameters();
        }
        
        [TestMethod]
        public virtual async Task ConstructorCall_InvalidParameters()
        {
            // Should throw any exception when constructor is called with invalid params.
            // The specific exceptions that can be thrown by the constructor (and ONLY the constructor)
            // are decided by the implementation, not the interface.
            var thrown = false;

            try
            {
                await CreateInstance_InvalidParameters();
            }
            catch (Exception)
            {
                thrown = true;
            }
            finally
            {
                Assert.IsTrue(thrown);
            }
        }
        
        [TestMethod]
        [AllEnumFlagCombinations(typeof(FileAccess))]
        public virtual async Task OpenStreamAndTryEachAccessMode(FileAccess accessMode)
        {
            var file = await CreateInstance_ValidParameters();

            if (accessMode == 0)
            {
                await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(() => file.OpenStreamAsync(accessMode));
                return;
            }

            await using var stream = await file.OpenStreamAsync(accessMode);

            if (accessMode.HasFlag(FileAccess.Read))
            {
                var bytes = await stream.ToBytesAsync();
                Assert.AreEqual(stream.Length, bytes.Length);
            }

            if (accessMode.HasFlag(FileAccess.Write))
            {
                stream.WriteByte(0);
            }
        }
        
        [TestMethod]
        [AllEnumFlagCombinations(typeof(FileAccess))]
        public virtual async Task OpenStreamWithEachAccessModeAndCancelToken(FileAccess accessMode)
        {
            var cancellationTokenSource = new CancellationTokenSource();

            var file = await CreateInstance_ValidParameters();

            if (accessMode == 0)
            {
                var task = Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(() => file.OpenStreamAsync(accessMode, cancellationTokenSource.Token));
                cancellationTokenSource.Cancel();

                await task;
                return;
            }

            cancellationTokenSource.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => file.OpenStreamAsync(accessMode, cancellationTokenSource.Token));
        }
    }

}