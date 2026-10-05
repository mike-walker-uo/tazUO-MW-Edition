using System;
using System.Runtime.Serialization;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class PetLoyaltyOwnershipTests : IDisposable
    {
        private readonly PlayerMobile _originalPlayer = World.Player;

        public PetLoyaltyOwnershipTests()
        {
            SetPlayer((PlayerMobile)FormatterServices.GetUninitializedObject(typeof(PlayerMobile)));
            World.Player.Serial = 1;
        }

        public void Dispose() => SetPlayer(_originalPlayer);
        private static void SetPlayer(PlayerMobile player) => typeof(World).GetProperty(nameof(World.Player)).SetValue(null, player);

        [Theory]
        [InlineData(false, NotorietyFlag.Innocent, false)]
        [InlineData(false, NotorietyFlag.Ally, false)]
        [InlineData(true, NotorietyFlag.Innocent, true)]
        [InlineData(true, NotorietyFlag.Ally, true)]
        [InlineData(true, NotorietyFlag.Enemy, false)]
        [InlineData(true, NotorietyFlag.Invulnerable, false)]
        public void Loyalty_needs_rename_permission_not_just_friendly_notoriety(
            bool renamable, NotorietyFlag notoriety, bool expected)
        {
            var pet = new Mobile(2) { IsRenamable = renamable, NotorietyFlag = notoriety };
            Assert.Equal(expected, PetLoyaltyAlertManager.IsNearbyPet(pet));
        }

        [Fact]
        public void Unowned_pet_overhead_and_ambiguous_broadcast_are_ignored()
        {
            var otherPet = new Mobile(2) { NotorietyFlag = NotorietyFlag.Ally };
            Assert.False(PetLoyaltyAlertManager.IsOwnPetMessage(Message(otherPet, "Fido looks unhappy")));
            Assert.False(PetLoyaltyAlertManager.IsOwnPetMessage(Message(null, "Fido looks unhappy")));
        }

        [Fact]
        public void Own_pet_overhead_and_explicit_private_reminder_remain_supported()
        {
            var ownPet = new Mobile(2) { IsRenamable = true, NotorietyFlag = NotorietyFlag.Ally };
            Assert.True(PetLoyaltyAlertManager.IsOwnPetMessage(Message(ownPet, "Fido looks unhappy")));
            World.Player.Followers = 1;
            Assert.True(PetLoyaltyAlertManager.IsOwnPetMessage(Message(null, "Your pet looks unhappy")));
            World.Player.Followers = 0;
            Assert.False(PetLoyaltyAlertManager.IsOwnPetMessage(Message(null, "Your pet looks unhappy")));
        }

        private static MessageEventArgs Message(Entity parent, string text) =>
            new MessageEventArgs(parent, text, "System", 0, MessageType.System, 0, TextType.SYSTEM);
    }
}
