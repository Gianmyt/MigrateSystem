using Migration.Infrastructure.models;
using Migration.Infrastructure.Utilities;

namespace TestProject
{
    public class MapperTest
    {

        [Fact]
        public void Map_ValidUser_ShouldMapCorrectly()
        {
            // Arrange
            var oldUser = new OldUser
            {
                Id = 1,
                FullName = "mario rossi",
                Mail = " Mario.Rossi@EMAIL.it ",
                Phone = "0039-333 1234567"
            };

            // Act
            var newUser = UserMapper.Map(oldUser);

            // Assert
            Assert.Equal("Mario", newUser.FirstName);
            Assert.Equal("Rossi", newUser.LastName);
            Assert.Equal("mario.rossi@email.it", newUser.Email);
            Assert.Equal("00393331234567", newUser.PhoneNumber);
            Assert.NotEqual(Guid.Empty, newUser.UserId);
            Assert.True(newUser.MigratedAt <= DateTime.UtcNow);
        }

        [Fact]
        public void Map_MissingFullName_ShouldThrow()
        {
            var oldUser = new OldUser
            {
                Id = 2,
                FullName = "",
                Mail = "test@email.com"
            };

            Assert.Throws<ArgumentException>(() => UserMapper.Map(oldUser));
        }

        [Fact]
        public void Map_InvalidEmail_ShouldThrow()
        {
            var oldUser = new OldUser
            {
                Id = 3,
                FullName = "Mario Rossi",
                Mail = "invalid-email"
            };

            Assert.Throws<ArgumentException>(() => UserMapper.Map(oldUser));
        }

        [Fact]
        public void Map_InvalidPhone_ShouldThrow()
        {
            var oldUser = new OldUser
            {
                Id = 4,
                FullName = "Mario Rossi",
                Mail = "mario.rossi@email.it",
                Phone = "abc123"
            };

            Assert.Throws<ArgumentException>(() => UserMapper.Map(oldUser));
        }

        [Fact]
        public void Map_NameWithoutLastName_ShouldFallbackToNA()
        {
            var oldUser = new OldUser
            {
                Id = 5,
                FullName = "Mario",
                Mail = "mario@email.it"
            };

            var newUser = UserMapper.Map(oldUser);

            Assert.Equal("Mario", newUser.FirstName);
            Assert.Equal("N/A", newUser.LastName);
        }
    }
}
