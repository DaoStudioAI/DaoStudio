using System;
using DaoStudio;
using DaoStudio.DBStorage.Factory;
using DaoStudio.Interfaces;
using DaoStudio.Services;
using DryIoc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Test.TestDaoStudio
{
    public class TestApplicationPathsServiceIntegration : IDisposable
    {
        private readonly Container _container;

        public TestApplicationPathsServiceIntegration()
        {
            _container = new Container();
            
            // Setup logging mock
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
                           .Returns(new Mock<ILogger>().Object);
            
            _container.RegisterInstance(mockLoggerFactory.Object);
            
            // Register the ApplicationPathsService using the same logic as DaoStudio
            DaoStudio.DaoStudioService.RegisterServices(_container);
        }

        public void Dispose()
        {
            _container?.Dispose();
        }

        [Fact]
        public void Container_ShouldResolveApplicationPathsService()
        {
            // Act
            var pathsService = _container.Resolve<IApplicationPathsService>();

            // Assert
            Assert.NotNull(pathsService);
            Assert.IsType<ApplicationPathsService>(pathsService);
        }

        [Fact]
        public void Container_ShouldResolveSameInstanceOfApplicationPathsService()
        {
            // Act
            var pathsService1 = _container.Resolve<IApplicationPathsService>();
            var pathsService2 = _container.Resolve<IApplicationPathsService>();

            // Assert
            Assert.NotNull(pathsService1);
            Assert.NotNull(pathsService2);
            Assert.Same(pathsService1, pathsService2); // Should be same instance (singleton)
        }

        [Fact]
        public void Container_StorageFactory_ShouldUsePathsServiceDatabasePath()
        {
            // Arrange
            var pathsService = _container.Resolve<IApplicationPathsService>();
            
            // Act
            var storageFactory = _container.Resolve<StorageFactory>();

            // Assert
            Assert.NotNull(storageFactory);
            Assert.Equal(pathsService.SettingsDatabasePath, storageFactory.DatabasePath);
        }
    }
}