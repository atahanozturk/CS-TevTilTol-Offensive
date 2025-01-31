using System;
using UnityEngine;
using UnityStandardAssets.CrossPlatformInput;
using UnityStandardAssets.Utility;
using Random = UnityEngine.Random;
using System.Collections;

namespace UnityStandardAssets.Characters.FirstPerson
{
    [RequireComponent(typeof (CharacterController))]
    [RequireComponent(typeof (AudioSource))]
    public class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private bool m_IsWalking;
        [SerializeField] private float m_WalkSpeed;
		[SerializeField] private float m_RunSpeed;
		[SerializeField] private float m_StutterMultiplier;
		[SerializeField] private float m_StutterRecover;
		[SerializeField] private float m_ShakeWildness;	//0.1f
		[SerializeField] private float m_ShakeTime;	//0.1f
		[SerializeField] private float m_ShakeViewShift; //1f
		[SerializeField] private float m_ShakeSideMult; //0.5f
        [SerializeField] [Range(0f, 1f)] private float m_RunstepLenghten;
        [SerializeField] private float m_JumpSpeed;
        [SerializeField] private float m_StickToGroundForce;
        [SerializeField] private float m_GravityMultiplier;
        [SerializeField] private MouseLook m_MouseLook;
        [SerializeField] private bool m_UseFovKick;
        [SerializeField] private FOVKick m_FovKick = new FOVKick();
        [SerializeField] private bool m_UseHeadBob;
        [SerializeField] private CurveControlledBob m_HeadBob = new CurveControlledBob();
        [SerializeField] private LerpControlledBob m_JumpBob = new LerpControlledBob();
        [SerializeField] private float m_StepInterval;
        [SerializeField] private AudioClip[] m_FootstepSounds;    // an array of footstep sounds that will be randomly selected from.
        [SerializeField] private AudioClip m_JumpSound;           // the sound played when character leaves the ground.
        [SerializeField] private AudioClip m_LandSound;           // the sound played when character touches back on ground.

        private Camera m_Camera;
        private bool m_Jump;
        private float m_YRotation;
        private Vector2 m_Input;
        private Vector3 m_MoveDir = Vector3.zero;
        private CharacterController m_CharacterController;
        private CollisionFlags m_CollisionFlags;
        private bool m_PreviouslyGrounded;
        private Vector3 m_OriginalCameraPosition;
        private float m_StepCycle;
        private float m_NextStep;
        private bool m_Jumping;
        private AudioSource m_AudioSource;

		int diff = -1;
        // Use this for initialization
        private void Start()
        {
			diff = PlayerPrefs.GetInt ("Diff", -1);
            m_CharacterController = GetComponent<CharacterController>();
            m_Camera = Camera.main;
            m_OriginalCameraPosition = m_Camera.transform.localPosition;
            m_FovKick.Setup(m_Camera);
            m_HeadBob.Setup(m_Camera, m_StepInterval);
            m_StepCycle = 0f;
            m_NextStep = m_StepCycle/2f;
            m_Jumping = false;
            m_AudioSource = GetComponent<AudioSource>();
			m_MouseLook.Init(transform , m_Camera.transform);

			def_WalkSpeed = m_WalkSpeed;
			def_RunSpeed = m_RunSpeed;
        }

		float def_WalkSpeed;
		float def_RunSpeed;
		public void GetHitStutter (float percent){
			m_WalkSpeed = m_WalkSpeed * (1f - (Mathf.Clamp(percent * m_StutterMultiplier,0f,1f)));
			m_RunSpeed = m_RunSpeed * (1f - (Mathf.Clamp(percent * m_StutterMultiplier,0f,1f)*0.5f));

			//print ("New walk speed: " + m_WalkSpeed.ToString ());
		}

		void OnDisable (){
			m_WalkSpeed = def_WalkSpeed;
			m_RunSpeed = def_RunSpeed;
		}


        // Update is called once per frame
        private void Update()
        {
			m_WalkSpeed = Mathf.MoveTowards (m_WalkSpeed, def_WalkSpeed, m_StutterRecover * Time.deltaTime);
			m_RunSpeed = Mathf.MoveTowards (m_RunSpeed, def_RunSpeed, m_StutterRecover * Time.deltaTime);
            RotateView();
            // the jump state needs to read here to make sure it is not missed
            if (!m_Jump)
            {
                m_Jump = CrossPlatformInputManager.GetButtonDown("Jump");
            }

            if (!m_PreviouslyGrounded && m_CharacterController.isGrounded)
            {
                StartCoroutine(m_JumpBob.DoBobCycle());
                PlayLandingSound();
                m_MoveDir.y = 0f;
                m_Jumping = false;
            }
            if (!m_CharacterController.isGrounded && !m_Jumping && m_PreviouslyGrounded)
            {
                m_MoveDir.y = 0f;
            }

            m_PreviouslyGrounded = m_CharacterController.isGrounded;
        }


        private void PlayLandingSound()
        {
            m_AudioSource.clip = m_LandSound;
            m_AudioSource.Play();
            m_NextStep = m_StepCycle + .5f;
        }

        private void FixedUpdate()
        {
            float speed;
            GetInput(out speed);
            // always move along the camera forward as it is the direction that it being aimed at
            Vector3 desiredMove = transform.forward*m_Input.y + transform.right*m_Input.x;

            // get a normal for the surface that is being touched to move along it
            RaycastHit hitInfo;
            Physics.SphereCast(transform.position, m_CharacterController.radius, Vector3.down, out hitInfo,
                               m_CharacterController.height/2f);
            desiredMove = Vector3.ProjectOnPlane(desiredMove, hitInfo.normal).normalized;

            m_MoveDir.x = desiredMove.x*speed;
            m_MoveDir.z = desiredMove.z*speed;


            if (m_CharacterController.isGrounded)
            {
                m_MoveDir.y = -m_StickToGroundForce;

                if (m_Jump)
                {
                    m_MoveDir.y = m_JumpSpeed;
                    PlayJumpSound();
                    m_Jump = false;
                    m_Jumping = true;
                }
            }
            else
            {
                m_MoveDir += Physics.gravity*m_GravityMultiplier*Time.fixedDeltaTime;
            }
            m_CollisionFlags = m_CharacterController.Move(m_MoveDir*Time.fixedDeltaTime);

            ProgressStepCycle(speed);
            UpdateCameraPosition(speed);
        }


        private void PlayJumpSound()
        {
            m_AudioSource.clip = m_JumpSound;
            m_AudioSource.Play();
        }


        private void ProgressStepCycle(float speed)
        {
            if (m_CharacterController.velocity.sqrMagnitude > 0 && (m_Input.x != 0 || m_Input.y != 0))
            {
                m_StepCycle += (m_CharacterController.velocity.magnitude + (speed*(m_IsWalking ? 1f : m_RunstepLenghten)))*
                             Time.fixedDeltaTime;
            }

            if (!(m_StepCycle > m_NextStep))
            {
                return;
            }

            m_NextStep = m_StepCycle + m_StepInterval;

            PlayFootStepAudio();
        }


        private void PlayFootStepAudio()
        {
            if (!m_CharacterController.isGrounded)
            {
                return;
            }
            // pick & play a random footstep sound from the array,
            // excluding sound at index 0
            int n = Random.Range(1, m_FootstepSounds.Length);
            m_AudioSource.clip = m_FootstepSounds[n];
            m_AudioSource.PlayOneShot(m_AudioSource.clip);
            // move picked sound to index 0 so it's not picked next time
            m_FootstepSounds[n] = m_FootstepSounds[0];
            m_FootstepSounds[0] = m_AudioSource.clip;
        }

		Vector3 cameraShakeAmount = Vector3.zero;
		Coroutine lastShake;

		public void CameraShake (float damage , float _time){
			if(lastShake != null)
			StopCoroutine (lastShake);
			lastShake =StartCoroutine (_CameraShake(damage,_time));
		}

		IEnumerator _CameraShake (float damage ,float _time){
			m_ShakeTime = _time * 2f/3f;
			float time = _time;
			float middle = m_ShakeTime / 2f;
			Vector3 randomDir = new Vector3 (Random.Range(-1f,1f)*m_ShakeSideMult,1f,-1f).normalized ;


			Vector3 randomRotVector = new Vector3 (Random.Range(-1.0f,1.0f)*m_ShakeSideMult,1f,0).normalized;


			m_MouseLook.ApplyRotation (transform, m_Camera.transform, m_ShakeViewShift * (damage/80f), 0);
			//print("--------------------");
			while (time > 0) {
				//float lerpMult = (1f - Mathf.Abs ((time - middle) / middle));
				float lerpMult = (time/m_ShakeTime);
				//print ("1f - ((" + time.ToString() + " - " + middle.ToString() + ") / " + middle.ToString() + ") = " + lerpMult.ToString());
				Vector3 lerpedPos = Vector3.Lerp (Vector3.zero, randomDir, lerpMult);

				cameraShakeAmount = 0.1f*lerpedPos* (damage/80f);

				m_MouseLook.m_CameraOffset = Quaternion.LookRotation (randomRotVector);
				m_MouseLook.m_CameraOffsetMagnitude = lerpMult * m_ShakeWildness * (damage/80f);

				time -= Time.deltaTime;
				time = Mathf.Clamp (time, 0, m_ShakeTime);
				yield return null;
			}
			cameraShakeAmount = Vector3.zero;
		}


        private void UpdateCameraPosition(float speed)
        {
            Vector3 newCameraPosition;
            if (!m_UseHeadBob)
            {
                return;
            }
            if (m_CharacterController.velocity.magnitude > 0 && m_CharacterController.isGrounded)
            {
                m_Camera.transform.localPosition =
                    m_HeadBob.DoHeadBob(m_CharacterController.velocity.magnitude +
                                      (speed*(m_IsWalking ? 1f : m_RunstepLenghten)));
                newCameraPosition = m_Camera.transform.localPosition;
                newCameraPosition.y = m_Camera.transform.localPosition.y - m_JumpBob.Offset();
            }
            else
            {
				newCameraPosition = m_OriginalCameraPosition;
                newCameraPosition.y = m_OriginalCameraPosition.y - m_JumpBob.Offset();
            }

			m_Camera.transform.localPosition = newCameraPosition+= cameraShakeAmount;
        }

		float lastSpeed = -1f;

        private void GetInput(out float speed)
        {
            // Read input
            float horizontal = CrossPlatformInputManager.GetAxis("Horizontal");
            float vertical = CrossPlatformInputManager.GetAxis("Vertical");

            bool waswalking = m_IsWalking;

#if !MOBILE_INPUT
            // On standalone builds, walk/run speed is modified by a key press.
            // keep track of whether or not the character is walking or running
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.LeftAlt))
                m_IsWalking = false;
            else
                m_IsWalking = true;
            //m_IsWalking = !Input.GetKey(KeyCode.LeftShift);
           
#endif
			if (!m_IsWalking && m_Input.magnitude > 0)
				BroadcastMessage ("RunAnim", m_RunSpeed);
			else
				BroadcastMessage ("StopRunAnim");

            // set the desired speed to be walking or running
            speed = m_IsWalking ? m_WalkSpeed : m_RunSpeed;
			lastSpeed = speed;
			if (diff == 4)
				speed *= 1.2f;
            m_Input = new Vector2(horizontal, vertical);



            // normalize input if it exceeds 1 in combined length:
            if (m_Input.sqrMagnitude > 1)
            {
                m_Input.Normalize();
            }

            // handle speed change to give an fov kick
            // only if the player is going to a run, is running and the fovkick is to be used
            if (m_IsWalking != waswalking && m_UseFovKick && m_CharacterController.velocity.sqrMagnitude > 0)
            {
                StopAllCoroutines();
                StartCoroutine(!m_IsWalking ? m_FovKick.FOVKickUp() : m_FovKick.FOVKickDown());
            }
        }


        private void RotateView()
        {
            m_MouseLook.LookRotation (transform, m_Camera.transform);
        }


        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody body = hit.collider.attachedRigidbody;
            //dont move the rigidbody if the character is on top of it
            if (m_CollisionFlags == CollisionFlags.Below)
            {
                return;
            }

            if (body == null || body.isKinematic)
            {
                return;
            }
            body.AddForceAtPosition(m_CharacterController.velocity*0.1f, hit.point, ForceMode.Impulse);
        }

        public void ChangeMouseSensitivity(float X, float Y)
        {
            m_MouseLook.XSensitivity = X;
            m_MouseLook.YSensitivity = Y;
        }
    }
}
