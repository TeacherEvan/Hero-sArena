using System;
using Xunit;

namespace HeroArena.Tests
{
    public class CharacterJuiceStateTests
    {
        [Fact]
        public void InitialState_IsBaseline()
        {
            var juice = new CharacterJuiceState();
            Assert.Equal(1f, juice.ScaleX);
            Assert.Equal(1f, juice.ScaleY);
            Assert.Equal(0f, juice.WalkBob);
            Assert.Equal(0f, juice.TiltAngle);
        }

        [Fact]
        public void Moving_GeneratesWalkBobAndTilt()
        {
            var juice = new CharacterJuiceState();
            juice.Update(0.05f, true, 1.0f);
            
            Assert.True(Math.Abs(juice.WalkBob) > 0.001f);
            Assert.True(Math.Abs(juice.TiltAngle) > 0.001f);
        }

        [Fact]
        public void Stationary_DecaysWalkBobAndTilt()
        {
            var juice = new CharacterJuiceState();
            juice.Update(0.1f, true, 1.0f);
            
            // Now stop moving
            for (int i = 0; i < 20; i++)
            {
                juice.Update(0.05f, false, 0f);
            }
            
            Assert.True(Math.Abs(juice.WalkBob) < 0.05f);
            Assert.True(Math.Abs(juice.TiltAngle) < 0.01f);
        }

        [Fact]
        public void DashStretch_ElongatesX_CompressesY_AndReturnsToBaseline()
        {
            var juice = new CharacterJuiceState();
            juice.ApplyDashStretch(1.4f, 0.7f);
            
            Assert.Equal(1.4f, juice.ScaleX);
            Assert.Equal(0.7f, juice.ScaleY);
            
            // Advance spring physics
            for (int i = 0; i < 40; i++)
            {
                juice.Update(0.016f, false);
            }
            
            // After spring settles, scales return close to 1.0
            Assert.InRange(juice.ScaleX, 0.98f, 1.02f);
            Assert.InRange(juice.ScaleY, 0.98f, 1.02f);
        }

        [Fact]
        public void AttackRecoil_CompressesX_ExpandsY()
        {
            var juice = new CharacterJuiceState();
            juice.ApplyAttackRecoil(0.85f, 1.15f);
            
            Assert.Equal(0.85f, juice.ScaleX);
            Assert.Equal(1.15f, juice.ScaleY);
            
            for (int i = 0; i < 40; i++)
            {
                juice.Update(0.016f, false);
            }
            
            Assert.InRange(juice.ScaleX, 0.98f, 1.02f);
            Assert.InRange(juice.ScaleY, 0.98f, 1.02f);
        }

        [Fact]
        public void ImpactFlinch_ScalesUp_AndReturnsToBaseline()
        {
            var juice = new CharacterJuiceState();
            juice.ApplyImpactFlinch();
            
            Assert.Equal(1.15f, juice.ScaleX);
            Assert.Equal(1.15f, juice.ScaleY);
            
            for (int i = 0; i < 40; i++)
            {
                juice.Update(0.016f, false);
            }
            
            Assert.InRange(juice.ScaleX, 0.98f, 1.02f);
            Assert.InRange(juice.ScaleY, 0.98f, 1.02f);
        }

        [Fact]
        public void Reset_RestoresAllValuesToDefaults()
        {
            var juice = new CharacterJuiceState();
            juice.ApplyDashStretch(1.5f, 0.5f);
            juice.Update(0.05f, true, 1.0f);
            
            juice.Reset();
            
            Assert.Equal(1f, juice.ScaleX);
            Assert.Equal(1f, juice.ScaleY);
            Assert.Equal(0f, juice.WalkBob);
            Assert.Equal(0f, juice.TiltAngle);
        }

        [Fact]
        public void LargeDeltaTime_Clamped_NoNaN()
        {
            var juice = new CharacterJuiceState();
            juice.ApplyDashStretch();
            juice.Update(1.0f, true); // Massive delta time
            
            Assert.False(float.IsNaN(juice.ScaleX));
            Assert.False(float.IsNaN(juice.ScaleY));
            Assert.False(float.IsInfinity(juice.ScaleX));
            Assert.False(float.IsInfinity(juice.ScaleY));
        }
    }
}
