﻿// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using Microsoft.Azure.Mobile.Server.Config;
using Microsoft.Azure.Mobile.Server.Tables;
using Microsoft.Azure.Mobile.Server.Tables.Config;
using Microsoft.Azure.Mobile.Server.TestModels;
using Moq;
using TestUtilities;
using Xunit;

namespace Microsoft.Azure.Mobile.Server
{
    public class EntityDomainManagerTests
    {
        private const string UpdatedCategory = "你好世界";
        private const string ConnectionStringName = "MS_TableConnectionString";
        private const string Collection = "TestCollection";

        private HttpConfiguration config;
        private HttpRequestMessage request;
        private MovieContext context;
        private EntityDomainManagerMock manager;

        public EntityDomainManagerTests()
        {
            this.config = new HttpConfiguration();
            new MobileAppConfiguration().AddTables(new MobileAppTableConfiguration().AddEntityFramework());

            var provider = new Mock<IMobileAppSettingsProvider>();
            provider.Setup(p => p.GetMobileAppSettings()).Returns(new MobileAppSettingsDictionary());
            this.config.SetMobileAppSettingsProvider(provider.Object);

            this.request = new HttpRequestMessage(HttpMethod.Get, new Uri("http://localhost"));
            this.request.SetConfiguration(this.config);
            this.context = new MovieContext();
            this.manager = new EntityDomainManagerMock(this);
            this.manager.Reset();
        }

        [Fact]
        public void Context_Roundtrips()
        {
            DbContext roundtrips = new DbContext(ConnectionStringName);
            PropertyAssert.Roundtrips(this.manager, m => m.Context, PropertySetter.NullThrows, defaultValue: this.context, roundtripValue: roundtrips);
        }

        [Fact]
        public void Request_Roundtrips()
        {
            HttpRequestMessage roundtrips = new HttpRequestMessage();
            PropertyAssert.Roundtrips(this.manager, m => m.Request, PropertySetter.NullThrows, defaultValue: this.request, roundtripValue: roundtrips);
        }

        [Fact]
        public async Task QueryAsync_Throws_NotImplemented()
        {
            NotImplementedException ex = await Assert.ThrowsAsync<NotImplementedException>(() => ((IDomainManager<Movie>)this.manager).QueryAsync((ODataQueryOptions)null));
            Assert.Contains("The 'EntityDomainManagerMock' domain manager only supports 'IQueryable' for querying data. Please use the 'Query' method instead'.", ex.Message);
        }

        [Fact]
        public async Task LookupAsync_Throws_NotImplemented()
        {
            NotImplementedException ex = await Assert.ThrowsAsync<NotImplementedException>(() => ((IDomainManager<Movie>)this.manager).LookupAsync((string)null));
            Assert.Contains("The 'EntityDomainManagerMock' domain manager only supports 'IQueryable' for looking up data. Please use the 'Lookup' method instead'.", ex.Message);
        }

        [Fact]
        public async Task Query_ReturnsData()
        {
            // Arrange
            Collection<Movie> movies = TestData.Movies;

            foreach (Movie movie in movies)
            {
                movie.Id = null;
                movie.CreatedAt = null;
                movie.UpdatedAt = null;

                Movie result = await this.manager.InsertAsync(movie);

                Assert.Equal(movie.Name, result.Name);

                Assert.NotNull(result.Id);
                Assert.NotNull(result.CreatedAt);
                Assert.NotNull(result.UpdatedAt);
            }

            // Act
            IEnumerable<Movie> actual = this.manager.Query().ToArray();

            // Assert
            Assert.Equal(movies.Count, actual.Count());
        }

        [Fact]
        public async Task Query_DoesNotReturnSoftDeletedRows_IfIncludeDeletedIsFalse()
        {
            // Arrange
            List<Movie> movies = TestData.Movies.Take(2).ToList();

            await this.manager.InsertAsync(movies[0]);
            movies[1].Deleted = true;
            await this.manager.InsertAsync(movies[1]);

            this.manager.IncludeDeleted = false;

            // Act
            IEnumerable<Movie> actual = this.manager.Query().ToArray();

            // Assert
            Assert.Single(actual);
            Assert.Equal(actual.First().Id, movies[0].Id);
        }

        [Fact]
        public async Task Lookup_ReturnsData()
        {
            // Arrange
            Collection<Movie> movies = TestData.Movies;
            string id = null;

            foreach (Movie movie in movies)
            {
                movie.Id = null;
                movie.CreatedAt = null;
                movie.UpdatedAt = null;

                Movie result = await this.manager.InsertAsync(movie);

                Assert.Equal(movie.Name, result.Name);

                Assert.NotNull(result.Id);
                Assert.NotNull(result.CreatedAt);
                Assert.NotNull(result.UpdatedAt);

                if (id == null)
                {
                    id = result.Id;
                }
            }

            // Act
            SingleResult<Movie> actual = this.manager.Lookup(id);

            // Assert
            Assert.Equal(id, actual.Queryable.First().Id);
        }

        [Fact]
        public async Task Lookup_ReturnsSoftDeletedRecord_IfIncludeDeletedIsTrue()
        {
            // Arrange
            Movie movie = TestData.Movies.First();
            movie.Id = Guid.NewGuid().ToString("N");
            movie.CreatedAt = null;
            movie.UpdatedAt = null;

            await this.manager.InsertAsync(movie);
            this.manager.EnableSoftDelete = true;

            // Act
            bool result = await this.manager.DeleteAsync(movie.Id);

            this.manager.IncludeDeleted = true;
            Movie lookedup = this.manager.Lookup(movie.Id).Queryable.First();

            Assert.Equal(movie.Id, lookedup.Id);
            Assert.True(lookedup.Deleted);
            Assert.Equal(movie.Name, lookedup.Name);
            Assert.Equal(movie.Category, lookedup.Category);
        }

        [Fact]
        public async Task Lookup_DoesNotReturnSoftDeletedRecord_IfIncludeDeletedIsFalse()
        {
            // Arrange
            Movie movie = TestData.Movies.First();
            movie.Id = Guid.NewGuid().ToString("N");
            movie.CreatedAt = null;
            movie.UpdatedAt = null;

            await this.manager.InsertAsync(movie);
            this.manager.EnableSoftDelete = true;

            // Act
            bool result = await this.manager.DeleteAsync(movie.Id);

            this.manager.IncludeDeleted = false;
            Movie lookedup = this.manager.Lookup(movie.Id).Queryable.FirstOrDefault();

            Assert.Null(lookedup);
        }

        [Fact]
        public async Task InsertAsync_InsertsDataWithNoIdAndUpdatesTimestamp()
        {
            // Arrange
            Collection<Movie> movies = TestData.Movies;

            foreach (Movie movie in movies)
            {
                movie.Id = null;
                movie.CreatedAt = null;
                movie.UpdatedAt = null;

                Movie result = await this.manager.InsertAsync(movie);

                Assert.Equal(movie.Name, result.Name);

                Assert.NotNull(result.Id);
                Assert.NotNull(result.CreatedAt);
                Assert.NotNull(result.UpdatedAt);
            }
        }

        [Fact]
        public async Task InsertAsync_InsertsDataWithValidIdAndUpdatesTimestamp()
        {
            // Arrange
            Collection<Movie> movies = TestData.Movies;

            foreach (Movie movie in movies)
            {
                string id = Guid.NewGuid().ToString("N");
                movie.Id = id;
                movie.CreatedAt = null;
                movie.UpdatedAt = null;

                Movie result = await this.manager.InsertAsync(movie);

                Assert.Equal(movie.Name, result.Name);

                Assert.Equal(id, result.Id);
                Assert.NotNull(result.CreatedAt);
                Assert.NotNull(result.UpdatedAt);
            }
        }

        [Fact]
        public async Task InsertAsync_Throws_BadRequest_IfDataValidationFails()
        {
            // Arrange
            Movie movie = TestData.Movies[0];

            string id = Guid.NewGuid().ToString("N");
            movie.Id = id;
            movie.RunTimeMinutes = -1;
            movie.CreatedAt = null;
            movie.UpdatedAt = null;

            HttpResponseException ex = await AssertEx.ThrowsAsync<HttpResponseException>(async () => await this.manager.InsertAsync(movie));
            Assert.Equal(HttpStatusCode.BadRequest, ex.Response.StatusCode);
        }

        [Fact]
        public async Task InsertAsync_Throws_Conflict_IfDuplicateKeys()
        {
            // Arrange
            string id = Guid.NewGuid().ToString("N");

            Movie movie = TestData.Movies[0];
            movie.Id = id;

            Movie result = await this.manager.InsertAsync(movie);

            // Act
            HttpResponseException ex = await AssertEx.ThrowsAsync<HttpResponseException>(async () => await this.manager.InsertAsync(movie));
            HttpError error;
            ex.Response.TryGetContentValue(out error);

            Assert.Equal(HttpStatusCode.Conflict, ex.Response.StatusCode);
            Assert.Contains("The operation failed due to a conflict: 'Violation of PRIMARY KEY constraint 'PK_dbo.Movies'. Cannot insert duplicate key in object 'dbo.Movies'. The duplicate key value is ", error.Message);
        }

        [Fact]
        public async Task InsertAsync_Throws_Bad_Request_IfKeyIsTooLong()
        {
            // Arrange
            string id = Guid.NewGuid().ToString("N");

            Movie movie = TestData.Movies[0];
            movie.Id = string.Empty;
            for (int cnt = 0; cnt < 16; cnt++)
            {
                movie.Id += id;
            }

            // Act
            HttpResponseException ex = await AssertEx.ThrowsAsync<HttpResponseException>(async () => await this.manager.InsertAsync(movie));
            HttpError error;
            ex.Response.TryGetContentValue(out error);

            Assert.Equal(HttpStatusCode.BadRequest, ex.Response.StatusCode);
            Assert.Contains("The entity submitted was invalid: Validation error on property 'Id': The field Id must be a string or array type with a maximum length of '128'.", error.Message);
        }

        [Fact]
        public async Task UpdateAsync_UpdatesCurrentValues()
        {
            // Arrange
            Collection<Movie> movies = TestData.Movies;

            foreach (Movie movie in movies)
            {
                string id = Guid.NewGuid().ToString("N");
                movie.Id = id;
                movie.CreatedAt = null;
                movie.UpdatedAt = null;

                await this.manager.InsertAsync(movie);

                Delta<Movie> patch = new Delta<Movie>();
                patch.TrySetPropertyValue("Category", UpdatedCategory);
                Movie result = await this.manager.UpdateAsync(id, patch);

                Assert.Equal(id, result.Id);
                Assert.Equal(UpdatedCategory, result.Category);
                Assert.NotNull(result.UpdatedAt);
            }
        }

        [Fact]
        public async Task UpdateAsync_UpdatesCurrentValues_WhenItemIsSoftDeleted_AndIncludeDeletedIsTrue()
        {
            // Arrange
            Movie movie = TestData.Movies.First();
            movie.Deleted = true;
            Movie insertedMovie = await this.manager.InsertAsync(movie);

            // Act
            Delta<Movie> patch = new Delta<Movie>();
            this.manager.IncludeDeleted = true;
            patch.TrySetPropertyValue("Category", UpdatedCategory);
            Movie result = await this.manager.UpdateAsync(movie.Id, patch);

            // Assert
            Assert.Equal(movie.Id, result.Id);
            Assert.Equal(UpdatedCategory, result.Category);
            Assert.True(result.Deleted);
        }

        [Fact]
        public async Task UpdateAsync_Throws_NotFound_IfIdNotFound()
        {
            // Arrange
            string id = Guid.NewGuid().ToString("N");
            Delta<Movie> patch = new Delta<Movie>();

            // Act/Assert
            this.manager.IncludeDeleted = true;
            HttpResponseException ex = await AssertEx.ThrowsAsync<HttpResponseException>(async () => await this.manager.UpdateAsync(id, patch));
            Assert.Equal(HttpStatusCode.NotFound, ex.Response.StatusCode);

            this.manager.IncludeDeleted = false;

            ex = await AssertEx.ThrowsAsync<HttpResponseException>(async () => await this.manager.UpdateAsync(id, patch));
            Assert.Equal(HttpStatusCode.NotFound, ex.Response.StatusCode);
        }

        [Fact]
        public async Task UpdateAsync_Throws_NotFound_IfItemIsSoftDeleted()
        {
            // Arrange
            Movie movie = TestData.Movies.First();
            movie.Deleted = true;
            Movie insertedMovie = await this.manager.InsertAsync(movie);

            Delta<Movie> patch = new Delta<Movie>();

            this.manager.IncludeDeleted = false;

            // Act/Assert
            HttpResponseException ex = await AssertEx.ThrowsAsync<HttpResponseException>(async () => await this.manager.UpdateAsync(movie.Id, patch));
            Assert.Equal(HttpStatusCode.NotFound, ex.Response.StatusCode);
        }

        [Fact]
        public async Task UpdateAsync_Throws_Conflict_IfVersionMismatch()
        {
            // Arrange
            Collection<Movie> movies = new Collection<Movie>();
            foreach (Movie movie in TestData.Movies)
            {
                Movie insertedMovie = await this.manager.InsertAsync(movie);
                movies.Add(insertedMovie);
            }

            foreach (Movie movie in movies)
            {
                this.context = new MovieContext();
                EntityDomainManagerMock updateDomainManager = new EntityDomainManagerMock(this);

                Delta<Movie> patch = new Delta<Movie>();
                patch.TrySetPropertyValue("Category", UpdatedCategory);
                patch.TrySetPropertyValue("Version", Encoding.UTF8.GetBytes("Unknown"));

                HttpResponseException ex = await AssertEx.ThrowsAsync<HttpResponseException>(async () => await updateDomainManager.UpdateAsync(movie.Id, patch));
                Movie conflict;
                ex.Response.TryGetContentValue<Movie>(out conflict);

                Assert.Equal(HttpStatusCode.Conflict, ex.Response.StatusCode);
                Assert.Equal(movie.Category, conflict.Category);
                Assert.Equal(movie.Version, conflict.Version);
            }
        }

        [Fact]
        public async Task ReplaceAsync_ReplacesData()
        {
            // Arrange
            const string Category = "你好世界";
            Collection<Movie> movies = TestData.Movies;

            foreach (Movie movie in movies)
            {
                string id = Guid.NewGuid().ToString("N");
                movie.Id = id;
                movie.CreatedAt = null;
                movie.UpdatedAt = null;

                await this.manager.InsertAsync(movie);

                movie.Category = Category;

                Movie result = await this.manager.ReplaceAsync(id, movie);

                Assert.Equal(id, result.Id);
                Assert.Equal(Category, result.Category);
                Assert.NotNull(result.CreatedAt);
            }
        }

        [Fact]
        public async Task ReplaceAsync_Throws_BadRequest_IfIdNotFound()
        {
            // Arrange
            string id = Guid.NewGuid().ToString("N");
            Movie movie = new Movie();

            // Act/Assert
            HttpResponseException ex = await AssertEx.ThrowsAsync<HttpResponseException>(async () => await this.manager.ReplaceAsync(id, movie));
            Assert.Equal(HttpStatusCode.BadRequest, ex.Response.StatusCode);
        }

        [Fact]
        public async Task ReplaceAsync_Throws_BadRequest_IfInvalidId()
        {
            // Arrange
            Movie movie = new Movie();

            // Act/Assert
            HttpResponseException ex = await AssertEx.ThrowsAsync<HttpResponseException>(async () => await this.manager.ReplaceAsync("invalid", movie));
            Assert.Equal(HttpStatusCode.BadRequest, ex.Response.StatusCode);
        }

        [Fact]
        public async Task ReplaceAsync_Throws_Conflict_IfVersionMismatch()
        {
            // Arrange
            Collection<Movie> movies = new Collection<Movie>();
            foreach (Movie movie in TestData.Movies)
            {
                Movie insertedMovie = await this.manager.InsertAsync(movie);
                movies.Add(insertedMovie);
            }

            // Create new context to avoid an exception saying: Attaching an entity of type 'Microsoft.Azure.Mobile.Server.TestModels.MovieModel'
            // failed because another entity of the same type already has the same primary key value. This can happen when using the 'Attach' method or
            // setting the state of an entity to 'Unchanged' or 'Modified' if any entities in the graph have conflicting key values. This may be because
            // some entities are new and have not yet received database-generated key values. In this case use the 'Add' method or the 'Added' entity
            // state to track the graph and then set the state of non-new entities to 'Unchanged' or 'Modified' as appropriate.
            // This condition won't apply when running the service as you always get a new context.
            foreach (Movie movie in movies)
            {
                this.context = new MovieContext();
                EntityDomainManagerMock replaceDomainManager = new EntityDomainManagerMock(this);

                string originalCategory = movie.Category;
                byte[] originalVersion = movie.Version;
                movie.Category = UpdatedCategory;
                movie.Version = Encoding.UTF8.GetBytes("Unknown");

                HttpResponseException ex = await AssertEx.ThrowsAsync<HttpResponseException>(async () => await replaceDomainManager.ReplaceAsync(movie.Id, movie));
                Movie conflict;
                ex.Response.TryGetContentValue<Movie>(out conflict);

                Assert.Equal(HttpStatusCode.Conflict, ex.Response.StatusCode);
                Assert.Equal(originalCategory, conflict.Category);
                Assert.Equal(originalVersion, conflict.Version);
            }
        }

        [Fact]
        public async Task DeleteAsync_DeletesData()
        {
            // Arrange
            Collection<Movie> movies = TestData.Movies;

            foreach (Movie movie in movies)
            {
                string id = Guid.NewGuid().ToString("N");
                movie.Id = id;
                movie.CreatedAt = null;
                movie.UpdatedAt = null;

                await this.manager.InsertAsync(movie);

                bool result = await this.manager.DeleteAsync(id);

                Assert.True(result);
            }
        }

        [Fact]
        public async Task DeleteAsync_MarksAsDeleted_IfSoftDeleteIsTrue()
        {
            // Arrange
            Movie movie = TestData.Movies.First();
            movie.Id = Guid.NewGuid().ToString("N");
            movie.CreatedAt = null;
            movie.UpdatedAt = null;

            await this.manager.InsertAsync(movie);
            this.manager.IncludeDeleted = false;
            this.manager.EnableSoftDelete = true;

            // Act
            bool result = await this.manager.DeleteAsync(movie.Id);

            // Assert
            Assert.True(result);

            Movie lookedup = this.manager.Lookup(movie.Id).Queryable.FirstOrDefault();
            Assert.Null(lookedup);

            this.manager.IncludeDeleted = true;
            lookedup = this.manager.Lookup(movie.Id).Queryable.First();

            Assert.Equal(movie.Id, lookedup.Id);
            Assert.True(lookedup.Deleted);
            Assert.Equal(movie.Name, lookedup.Name);
            Assert.Equal(movie.Category, lookedup.Category);
        }

        [Fact]
        public async Task DeleteAsync_ReturnsFalse_IfIdNotFound()
        {
            // Assert
            string id = Guid.NewGuid().ToString("N");

            // Act
            bool result = await this.manager.DeleteAsync(id);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task DeleteAsync_Throws_Conflict_IfVersionMismatch()
        {
            // Arrange
            Collection<Movie> movies = new Collection<Movie>();
            foreach (Movie movie in TestData.Movies)
            {
                Movie insertedMovie = await this.manager.InsertAsync(movie);
                movies.Add(insertedMovie);
            }

            this.request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse("\"QUJDREVG\""));
            foreach (Movie movie in movies)
            {
                this.context = new MovieContext();
                EntityDomainManagerMock deleteDomainManager = new EntityDomainManagerMock(this);

                HttpResponseException ex = await AssertEx.ThrowsAsync<HttpResponseException>(async () => await deleteDomainManager.DeleteAsync(movie.Id));
                Movie conflict;
                ex.Response.TryGetContentValue<Movie>(out conflict);

                Assert.Equal(HttpStatusCode.PreconditionFailed, ex.Response.StatusCode);
                Assert.Equal(movie.Version, conflict.Version);
            }
        }

        [Fact]
        public async Task UndeleteAsync_SetsDeletedToFalse()
        {
            // Arrange
            Movie movie = TestData.Movies.First();
            movie.Id = Guid.NewGuid().ToString("N");
            movie.CreatedAt = null;
            movie.UpdatedAt = null;
            movie.Deleted = true;

            movie = await this.manager.InsertAsync(movie);
            Assert.True(movie.Deleted);

            // Act
            movie = await this.manager.UndeleteAsync(movie.Id, null);

            // Assert
            Assert.False(movie.Deleted);
        }

        [Fact]
        public async Task UndeleteAsync_UpdatesAndUndeletes()
        {
            // Arrange
            Movie movie = TestData.Movies.First();
            movie.Id = Guid.NewGuid().ToString("N");
            movie.CreatedAt = null;
            movie.UpdatedAt = null;
            movie.Deleted = true;

            movie = await this.manager.InsertAsync(movie);
            Assert.True(movie.Deleted);

            // Act
            var delta = new Delta<Movie>();
            delta.TrySetPropertyValue("Name", "abc");
            movie = await this.manager.UndeleteAsync(movie.Id, delta);

            // Assert
            Assert.False(movie.Deleted);
            Assert.Equal("abc", movie.Name);
        }

        [Fact]
        public async Task UndeleteAsync_ThrowsNotFound_IfIdNotFound()
        {
            // Act/Assert
            HttpResponseException ex = await AssertEx.ThrowsAsync<HttpResponseException>(async () => await this.manager.UndeleteAsync("abc", null));
            Assert.Equal(HttpStatusCode.NotFound, ex.Response.StatusCode);
        }

        private class EntityDomainManagerMock : EntityDomainManager<Movie>
        {
            public EntityDomainManagerMock(EntityDomainManagerTests parent)
                : base(parent.context, parent.request)
            {
            }

            public void Reset()
            {
                this.Context.Database.Delete();
            }
        }
    }
}