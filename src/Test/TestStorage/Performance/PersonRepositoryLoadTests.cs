using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;
using TestStorage.Performance;
using Xunit;
using Xunit.Abstractions;

namespace TestStorage.Performance
{
    /// <summary>
    /// Load tests for IPersonRepository focusing on performance under high load conditions
    /// </summary>
    public class PersonRepositoryLoadTests : BaseLoadTest
    {
        private IPersonRepository? _personRepository;

        public PersonRepositoryLoadTests(ITestOutputHelper output) : base(output)
        {
        }

        private async Task<IPersonRepository> GetPersonRepositoryAsync()
        {
            if (_personRepository == null)
            {
                await InitializeStorageAsync();
                _personRepository = await StorageFactory!.GetPersonRepositoryAsync();
            }
            return _personRepository;
        }

        [Fact]
        public async Task BulkPersonCreationTest_Small()
        {
            var repository = await GetPersonRepositoryAsync();
            var testPersons = TestDataGenerator.GeneratePersons(TestDataGenerator.Scenarios.Small.PersonsCount);

            await RunBulkOperationStressTestAsync<Person>(
                "Bulk Create Persons (Small)",
                async () =>
                {
                    var results = new List<Person>();
                    foreach (var person in testPersons)
                    {
                        var created = await repository.CreatePersonAsync(person);
                        results.Add(created);
                    }
                    return results;
                },
                testPersons.Count);

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task BulkPersonCreationWithImagesTest_Medium()
        {
            var repository = await GetPersonRepositoryAsync();
            var testPersons = TestDataGenerator.GeneratePersons(
                TestDataGenerator.Scenarios.Medium.PersonsCount, 
                includeImages: true);

            var result = await RunBulkOperationStressTestAsync<Person>(
                "Bulk Create Persons with Images (Medium)",
                async () =>
                {
                    var results = new List<Person>();
                    foreach (var person in testPersons)
                    {
                        var created = await repository.CreatePersonAsync(person);
                        results.Add(created);
                    }
                    return results;
                },
                testPersons.Count);

            // Assert performance requirements - should create 2000 persons with images in under 45 seconds
            AssertPerformanceRequirements(result, TimeSpan.FromSeconds(45));
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ConcurrentModelUpdatesTest()
        {
            var repository = await GetPersonRepositoryAsync();
            var testPersons = TestDataGenerator.GeneratePersons(100);

            // First, create all persons
            var createdPersons = new List<Person>();
            foreach (var person in testPersons)
            {
                var created = await repository.CreatePersonAsync(person);
                createdPersons.Add(created);
            }

            // Create concurrent operations to update model configurations
            var operations = new List<Func<Task<object>>>();

            foreach (var person in createdPersons)
            {
                // Update model ID
                var updatedPerson = person;
                updatedPerson.ModelId = $"updated-model-{DateTime.UtcNow.Ticks}";
                updatedPerson.LastModified = DateTime.UtcNow;
                
                operations.Add(async () => (object)(await repository.SavePersonAsync(updatedPerson)));
                
                // Add concurrent read operation
                operations.Add(async () => (object)(await repository.GetPersonAsync(updatedPerson.Id) ?? new Person()));
            }

            var result = await MeasureConcurrentPerformanceAsync(
                "Concurrent Model Updates",
                operations,
                maxConcurrency: 10);

            // Assert good concurrency performance
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 30.0, maxFailureRate: 0.03);
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task BatchGetPersonsByNamesPerformanceTest()
        {
            var repository = await GetPersonRepositoryAsync();
            var testPersons = TestDataGenerator.GeneratePersons(500, includeImages: true);

            // Create all persons
            var createdPersons = new List<Person>();
            foreach (var person in testPersons)
            {
                var created = await repository.CreatePersonAsync(person);
                createdPersons.Add(created);
            }

            // Test batch retrieval performance (should prevent N+1 queries)
            var personNames = createdPersons.Take(100).Select(p => p.Name).ToList();

            var batchResult = await MeasurePerformanceAsync(
                "Batch Get Persons by Names",
                async () =>
                {
                    var batchPersons = await repository.GetPersonsByNamesAsync(personNames, includeImage: true);
                    return batchPersons.ToList();
                });

            Assert.True(batchResult.Success);
            var batchPersons = (List<Person>)batchResult.Result!;
            Assert.Equal(personNames.Count, batchPersons.Count);

            // Compare with individual lookups (should be much slower)
            var individualResult = await MeasurePerformanceAsync(
                "Individual Get Persons by Names",
                async () =>
                {
                    var individualPersons = new List<Person?>();
                    foreach (var name in personNames)
                    {
                        var person = await repository.GetPersonByNameAsync(name);
                        individualPersons.Add(person);
                    }
                    return individualPersons;
                });

            Assert.True(individualResult.Success);
            
            // Batch operation should be significantly faster
            var batchTimeMs = batchResult.Duration.TotalMilliseconds;
            var individualTimeMs = individualResult.Duration.TotalMilliseconds;
            
            Output.WriteLine($"Batch operation: {batchTimeMs:F2}ms, Individual operations: {individualTimeMs:F2}ms");
            Output.WriteLine($"Batch is {(individualTimeMs / batchTimeMs):F1}x faster");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task LazyLoadingImagePerformanceTest()
        {
            var repository = await GetPersonRepositoryAsync();
            var testPersons = TestDataGenerator.GeneratePersons(200, includeImages: true);

            // Create persons with images
            var createdPersons = new List<Person>();
            foreach (var person in testPersons)
            {
                var created = await repository.CreatePersonAsync(person);
                createdPersons.Add(created);
            }

            // Test retrieval without images (should be faster)
            var withoutImagesResult = await MeasurePerformanceAsync(
                "Get All Persons Without Images",
                async () =>
                {
                    var persons = await repository.GetAllPersonsAsync(includeImage: false);
                    return persons.ToList();
                });

            Assert.True(withoutImagesResult.Success);
            var personsWithoutImages = (List<Person>)withoutImagesResult.Result!;
            Assert.All(personsWithoutImages, p => Assert.Null(p.Image));

            // Test retrieval with images (should be slower but include image data)
            var withImagesResult = await MeasurePerformanceAsync(
                "Get All Persons With Images",
                async () =>
                {
                    var persons = await repository.GetAllPersonsAsync(includeImage: true);
                    return persons.ToList();
                });

            Assert.True(withImagesResult.Success);
            var personsWithImages = (List<Person>)withImagesResult.Result!;
            Assert.All(personsWithImages.Where(p => createdPersons.Any(cp => cp.Id == p.Id)), 
                p => Assert.NotNull(p.Image));

            // Without images should be faster
            Output.WriteLine($"Without images: {withoutImagesResult.Duration.TotalMilliseconds:F2}ms, " +
                           $"With images: {withImagesResult.Duration.TotalMilliseconds:F2}ms");
            
            Assert.True(withoutImagesResult.Duration < withImagesResult.Duration,
                "Lazy loading should make queries without images faster");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ProviderBasedFilteringStressTest()
        {
            var repository = await GetPersonRepositoryAsync();
            var testPersons = TestDataGenerator.GeneratePersons(1000);

            // Create persons with various providers
            var createdPersons = new List<Person>();
            foreach (var person in testPersons)
            {
                var created = await repository.CreatePersonAsync(person);
                createdPersons.Add(created);
            }

            // Get unique provider names for testing
            var uniqueProviders = createdPersons.Select(p => p.ProviderName).Distinct().ToList();

            // Test concurrent provider-based filtering
            var filterOperations = uniqueProviders.Select<string, Func<Task<object>>>(providerName =>
                async () => (object)(await repository.GetPersonsByProviderNameAsync(providerName))).ToList();

            var result = await MeasureConcurrentPerformanceAsync(
                "Provider-Based Filtering Stress Test",
                filterOperations,
                maxConcurrency: 8);

            // Provider-based filtering should be efficient with proper indexing
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 15.0, maxFailureRate: 0.02);

            Output.WriteLine($"Tested filtering across {uniqueProviders.Count} different providers");
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task EnabledPersonsFilteringPerformanceTest()
        {
            var repository = await GetPersonRepositoryAsync();
            var testPersons = TestDataGenerator.GeneratePersons(800);

            // Create persons (mix of enabled/disabled)
            foreach (var person in testPersons)
            {
                await repository.CreatePersonAsync(person);
            }

            // Test enabled persons filtering with and without images
            var enabledWithoutImagesResult = await MeasurePerformanceAsync(
                "Get Enabled Persons Without Images",
                async () =>
                {
                    var persons = await repository.GetEnabledPersonsAsync(includeImage: false);
                    return persons.ToList();
                });

            var enabledWithImagesResult = await MeasurePerformanceAsync(
                "Get Enabled Persons With Images",
                async () =>
                {
                    var persons = await repository.GetEnabledPersonsAsync(includeImage: true);
                    return persons.ToList();
                });

            Assert.True(enabledWithoutImagesResult.Success);
            Assert.True(enabledWithImagesResult.Success);

            var enabledWithoutImages = (List<Person>)enabledWithoutImagesResult.Result!;
            var enabledWithImages = (List<Person>)enabledWithImagesResult.Result!;

            // Should return same count but different performance
            Assert.Equal(enabledWithoutImages.Count, enabledWithImages.Count);
            Assert.All(enabledWithoutImages, p => Assert.True(p.IsEnabled));
            Assert.All(enabledWithImages, p => Assert.True(p.IsEnabled));

            // Filtering should be reasonably fast
            Assert.True(enabledWithoutImagesResult.Duration < TimeSpan.FromSeconds(3),
                $"Enabled filtering without images took {enabledWithoutImagesResult.Duration.TotalSeconds:F2}s, expected < 3s");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task PersonNameExistenceCheckPerformanceTest()
        {
            var repository = await GetPersonRepositoryAsync();
            var testPersons = TestDataGenerator.GeneratePersons(500);

            // Create persons
            var createdPersons = new List<Person>();
            foreach (var person in testPersons)
            {
                var created = await repository.CreatePersonAsync(person);
                createdPersons.Add(created);
            }

            // Test name existence checks (should be very fast with unique index)
            var existingNames = createdPersons.Take(100).Select(p => p.Name).ToList();
            var nonExistentNames = Enumerable.Range(1, 100).Select(i => $"NonExistent_{i}").ToList();

            var result = await MeasurePerformanceAsync(
                "Person Name Existence Checks",
                async () =>
                {
                    var results = new List<bool>();
                    
                    // Check existing names
                    foreach (var name in existingNames)
                    {
                        results.Add(repository.PersonNameExists(name));
                    }
                    
                    // Check non-existent names
                    foreach (var name in nonExistentNames)
                    {
                        results.Add(repository.PersonNameExists(name));
                    }
                    
                    return results;
                });

            Assert.True(result.Success);
            var results = (List<bool>)result.Result!;
            
            // First 100 should be true, next 100 should be false
            Assert.All(results.Take(100), r => Assert.True(r));
            Assert.All(results.Skip(100), r => Assert.False(r));

            // Name existence checks should be very fast
            Assert.True(result.Duration < TimeSpan.FromSeconds(1),
                $"Name existence checks took {result.Duration.TotalSeconds:F2}s, expected < 1s");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ConcurrentPersonDeletionTest()
        {
            var repository = await GetPersonRepositoryAsync();
            var testPersons = TestDataGenerator.GeneratePersons(300, includeImages: true);

            // Create persons to delete
            var createdPersons = new List<Person>();
            foreach (var person in testPersons)
            {
                var created = await repository.CreatePersonAsync(person);
                createdPersons.Add(created);
            }

            // Test concurrent deletion performance
            var deleteOperations = createdPersons.Take(200).Select<Person, Func<Task<object>>>(person =>
                async () => (object)(await repository.DeletePersonAsync(person.Id))).ToList();

            var result = await MeasureConcurrentPerformanceAsync(
                "Concurrent Person Deletions",
                deleteOperations,
                maxConcurrency: 6);

            // Deletions should be reasonably fast
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 25.0, maxFailureRate: 0.02);

            // Verify some persons remain
            var remainingPersons = await repository.GetAllPersonsAsync(includeImage: false);
            var remainingCount = remainingPersons.Count();
            
            Output.WriteLine($"Remaining persons after deletion: {remainingCount}");
            Assert.True(remainingCount >= 100, "Should have some persons remaining after partial deletion");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task MemoryUsageUnderPersonLoadTest()
        {
            var repository = await GetPersonRepositoryAsync();
            
            // Test with progressively larger datasets and images
            var smallPersons = TestDataGenerator.GeneratePersons(50, includeImages: false);
            var mediumPersons = TestDataGenerator.GeneratePersons(100, includeImages: true);
            var largePersons = TestDataGenerator.GeneratePersons(50, includeImages: true, toolsPerPerson: 10, parametersPerPerson: 30);

            var smallResult = await MeasurePerformanceAsync(
                "Memory Test - Small Persons (No Images)",
                async () =>
                {
                    foreach (var person in smallPersons)
                    {
                        await repository.CreatePersonAsync(person);
                    }
                    return smallPersons.Count;
                });

            var mediumResult = await MeasurePerformanceAsync(
                "Memory Test - Medium Persons (With Images)",
                async () =>
                {
                    foreach (var person in mediumPersons)
                    {
                        await repository.CreatePersonAsync(person);
                    }
                    return mediumPersons.Count;
                });

            var largeResult = await MeasurePerformanceAsync(
                "Memory Test - Large Persons (Complex)",
                async () =>
                {
                    foreach (var person in largePersons)
                    {
                        await repository.CreatePersonAsync(person);
                    }
                    return largePersons.Count;
                });

            // Memory usage should scale reasonably
            Assert.True(smallResult.MemoryUsed < 15 * 1024 * 1024, // Less than 15MB
                $"Small persons used {smallResult.MemoryUsed / (1024 * 1024)}MB, expected < 15MB");
            
            Output.WriteLine($"Memory usage - Small: {smallResult.MemoryUsed / 1024}KB, " +
                           $"Medium: {mediumResult.MemoryUsed / 1024}KB, " +
                           $"Large: {largeResult.MemoryUsed / 1024}KB");

            await VerifyDatabaseIntegrityAsync();
        }
    }
}
