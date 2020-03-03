using UnityEngine;

namespace Player.ThirdPerson
{
	[RequireComponent(typeof(Rigidbody))]
	[RequireComponent(typeof(CapsuleCollider))]
	[RequireComponent(typeof(Animator))]
	internal sealed class ThirdPersonCharacter : MonoBehaviour
	{
		[SerializeField] private float m_MovingTurnSpeed = 360;
		[SerializeField] private float m_StationaryTurnSpeed = 180;
		[SerializeField] private float m_JumpPower = 12f;
		[Range(1f, 4f)][SerializeField] private float m_GravityMultiplier = 2f;
		[SerializeField] private float m_RunCycleLegOffset = 0.2f; 
		[SerializeField] private float m_MoveSpeedMultiplier = 1f;
		[SerializeField] private float m_AnimSpeedMultiplier = 1f;
		[SerializeField] private float m_GroundCheckDistance = 0.1f;

		private Rigidbody m_Rigidbody;
		private Animator m_Animator;
		private bool m_IsGrounded;
		private float m_OrigGroundCheckDistance;
		private const float k_Half = 0.5f;
		private float m_TurnAmount;
		private float m_ForwardAmount;
		private Vector3 m_GroundNormal;
		private float m_CapsuleHeight;
		private Vector3 m_CapsuleCenter;
		private CapsuleCollider m_Capsule;
		private bool m_Crouching;

		private void Start()
		{
			m_Animator = GetComponent<Animator>();
			m_Rigidbody = GetComponent<Rigidbody>();
			m_Capsule = GetComponent<CapsuleCollider>();
			m_CapsuleHeight = m_Capsule.height;
			m_CapsuleCenter = m_Capsule.center;
			m_OrigGroundCheckDistance = m_GroundCheckDistance;
		}

        public void Move(Vector3 move, bool crouch, bool jump)
		{
            // convert the world relative moveInput vector into a local-relative
			// turn amount and forward amount required to head in the desired
			// direction.
			if (move.magnitude > 1f)
            {
				move.Normalize();
            }

			move = transform.InverseTransformDirection(move);
			CheckGroundStatus();
			move = Vector3.ProjectOnPlane(move, m_GroundNormal);
			m_TurnAmount = Mathf.Atan2(move.x, move.z);
			m_ForwardAmount = move.z;

			ApplyExtraTurnRotation();

			// control and velocity handling is different when grounded and airborne:
			if (m_IsGrounded)
			{
				HandleGroundedMovement(crouch, jump);
			}
			else
			{
				HandleAirborneMovement();
			}

			ScaleCapsuleForCrouching(crouch);
			PreventStandingInLowHeadroom();
			UpdateAnimator(move);
		}

		private void ScaleCapsuleForCrouching(bool crouch)
		{
			if (m_IsGrounded && crouch)
			{
				if (m_Crouching) 
                    return;

				m_Capsule.height /= 2f;
				m_Capsule.center /= 2f;
				m_Crouching = true;
			}
			else
			{
                var radius = m_Capsule.radius;
                var crouchRay = new Ray(m_Rigidbody.position + k_Half * radius * Vector3.up, Vector3.up);
				var crouchRayLength = m_CapsuleHeight - radius * k_Half;

				if (Physics.SphereCast(crouchRay, m_Capsule.radius * k_Half, crouchRayLength, Physics.AllLayers, QueryTriggerInteraction.Ignore))
				{
					m_Crouching = true;
					return;
				}

				m_Capsule.height = m_CapsuleHeight;
				m_Capsule.center = m_CapsuleCenter;
				m_Crouching = false;
			}
		}

        private void PreventStandingInLowHeadroom()
		{
            if (m_Crouching) 
                return;

            var radius = m_Capsule.radius;
            var crouchRay = new Ray(m_Rigidbody.position + k_Half * radius * Vector3.up, Vector3.up);
            var crouchRayLength = m_CapsuleHeight - radius * k_Half;

            if (Physics.SphereCast(crouchRay, m_Capsule.radius * k_Half, crouchRayLength, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                m_Crouching = true;
            }
        }

        private void UpdateAnimator(Vector3 move)
		{
			m_Animator.SetFloat("Forward", m_ForwardAmount, 0.1f, Time.deltaTime);
			m_Animator.SetFloat("Turn", m_TurnAmount, 0.1f, Time.deltaTime);
			m_Animator.SetBool("Crouch", m_Crouching);
			m_Animator.SetBool("OnGround", m_IsGrounded);

			if (!m_IsGrounded)
			{
				m_Animator.SetFloat("Jump", m_Rigidbody.velocity.y);
			}

			// calculate which leg is behind, so as to leave that leg trailing in the jump animation
			// (This code is reliant on the specific run cycle offset in our animations,
			// and assumes one leg passes the other at the normalized clip times of 0.0 and 0.5)
			var runCycle = Mathf.Repeat(m_Animator.GetCurrentAnimatorStateInfo(0).normalizedTime + m_RunCycleLegOffset, 1);
			var jumpLeg = (runCycle < k_Half ? 1 : -1) * m_ForwardAmount;

			if (m_IsGrounded)
			{
				m_Animator.SetFloat("JumpLeg", jumpLeg);
			}

			// the anim speed multiplier allows the overall speed of walking/running to be tweaked in the inspector,
			// which affects the movement speed because of the root motion.
			if (m_IsGrounded && move.magnitude > 0)
			{
				m_Animator.speed = m_AnimSpeedMultiplier;
			}
			else
			{
				// don't use that while airborne
				m_Animator.speed = 1;
			}
		}

        private void HandleAirborneMovement()
		{
			// apply extra gravity from multiplier:
			var extraGravityForce = (Physics.gravity * m_GravityMultiplier) - Physics.gravity;
			m_Rigidbody.AddForce(extraGravityForce);

			m_GroundCheckDistance = m_Rigidbody.velocity.y < 0 ? m_OrigGroundCheckDistance : 0.01f;
		}

        private void HandleGroundedMovement(bool crouch, bool jump)
		{
            if (!jump || crouch || !m_Animator.GetCurrentAnimatorStateInfo(0).IsName("Grounded")) 
                return;

            var velocity = m_Rigidbody.velocity;
            velocity = new Vector3(velocity.x, m_JumpPower, velocity.z);
            m_Rigidbody.velocity = velocity;
            m_IsGrounded = false;
            m_Animator.applyRootMotion = false;
            m_GroundCheckDistance = 0.1f;
        }

        private void ApplyExtraTurnRotation()
		{
			var turnSpeed = Mathf.Lerp(m_StationaryTurnSpeed, m_MovingTurnSpeed, m_ForwardAmount);
			transform.Rotate(0, m_TurnAmount * turnSpeed * Time.deltaTime, 0);
		}

        public void OnAnimatorMove()
		{
			if (m_IsGrounded && Time.deltaTime > 0)
			{
				var v = (m_Animator.deltaPosition * m_MoveSpeedMultiplier) / Time.deltaTime;

				// we preserve the existing y part of the current velocity.
				v.y = m_Rigidbody.velocity.y;
				m_Rigidbody.velocity = v;
			}
		}

		private void CheckGroundStatus()
		{
            // 0.1f is a small offset to start the ray from inside the character
			// it is also good to note that the transform position in the sample assets is at the base of the character
			if (Physics.Raycast(transform.position + (Vector3.up * 0.1f), Vector3.down, out var hitInfo, m_GroundCheckDistance))
			{
				m_GroundNormal = hitInfo.normal;
				m_IsGrounded = true;
				m_Animator.applyRootMotion = true;
			}
			else
			{
				m_IsGrounded = false;
				m_GroundNormal = Vector3.up;
				m_Animator.applyRootMotion = false;
			}
		}
	}
}