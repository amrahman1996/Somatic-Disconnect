//Add Comments explaining purpose of blocks of code and unclear variables and such
//Fix Ammo System
using UnityEngine;
using UnityEngine.SceneManagement;
using ExtensionMethods;
public class CapsuleScript : MonoBehaviour {
	#region Variables
	public bool player;
	public bool gameMode;
	public float maxWalkSpeed = 15f;
	public float acceleration = 30f;
	public float accelerationHoriz = 30f;
	public float verToHorDeceleration = 0.5f;
	public float kick = 50; //Gives player acceleration a little kick to make movement more responsive
	public float kickBack = 2; //kicks rigidbodies under player backwards
	public float sprintSpeed = 2.5f;
	public float horizSprintRatio = 1f;
	public float timeToFullSprint = 0.4f;
	public float jumpHeight = 10f;
	public float jetpackFuel; //Defines how long holding spacebar will allow a higher jump
	public float airControlWalkRatio = .05f;
	public float maxWalkSlope = 75f; //The steepest slope where one can still walk
	public float maxJumpSlope = 50f; //The steepest slope where one can jump
	float currSlope;
	float jumpHold;
	public bool crouchToggle = true;
	public float crouchSpeed = 0.2f;
	public float maxCrouchWalkSpeed = 3f;
	public GameObject spine, crouchCheckSphere, footL, footR, handR;
	public Grounded groundCheck;
	public PlayerPhysics playerPhysics;
	public Gun currentGun, secondGun;
	public GunHolding gunHolding;
	float gunWeight;
	internal float velForward, velRight;
	Vector3 jumpVec;
	Vector3 fwdVec, rightVec;
	Vector3 tangentVec, tangentRightVec;
	bool unCrouch, isCrouching;

	float targetScaleV;
	float maxSpeed = 20f;
	internal float crouchAmount;
	float lastCrouchAmount;
	internal float sprint = 1f;
	bool crouchBeforeJump, isReadyToJump, isJumping;
	bool shouldJump;
	public float switchCheckDist = 1;
	public LayerMask switchCheckLayerMask;
	public float throwGunUpForce = 100;
	public float throwGunForwardsForce = 300;
	public bool bulletShells = true;
	public string[] ammoTypes = new string[3];
	public int[] maxAmmos = {500, 3000, 1000};
	internal int[] currentAmmos = {500, 3000, 1000};
	float eTime;
	internal bool grounded;
	internal bool walking;
	float vIn;
	float hIn;
	bool frictionZero;
	float jetpackFuelC;
	bool canJump;
	float sprintVol1;
	Rigidbody rb;
	Transform spineTF;
	#endregion
	void Walk(){
		fwdVec = Quaternion.FromToRotation(spineTF.forward, tangentVec) * (spineTF.forward * sprint * (acceleration - gunWeight * .7f) * vIn * Time.deltaTime) * (1 + currSlope / 100);
		rightVec = Quaternion.FromToRotation(spineTF.up, tangentRightVec) * (spineTF.up * sprint * (accelerationHoriz - gunWeight * .7f) * hIn * Time.deltaTime) * (1 + currSlope / 100);
		//			Debug.DrawRay(tf.position, fwdVec.normalized, Color.blue);
		//			Debug.DrawRay(tf.position, rightVec.normalized, Color.cyan);
		if(Mathf.Abs(vIn) > 0 && Mathf.Abs(hIn) > 0){
			if(Mathf.Abs(velForward) < maxSpeed * sprint / 1.5f){
				rb.velocity += fwdVec;
			}
			if(Mathf.Abs(velRight) < maxSpeed * sprint / 1.5f){
				rb.velocity += rightVec;
			}
		} else{
			if(Mathf.Abs(velForward) < maxSpeed * sprint){
				rb.velocity += fwdVec;
				if(Mathf.Abs(vIn) > 0.1f)
					rb.velocity -= spineTF.up * velRight * verToHorDeceleration / 10;
			}
			if(Mathf.Abs(velRight) < maxSpeed * sprint){
				rb.velocity += rightVec;
				if(Mathf.Abs(hIn) > 0.1f)
					rb.velocity -= spineTF.forward * velForward * verToHorDeceleration / 10;
			}
		}
		if((Mathf.Abs(vIn) > 0 || Mathf.Abs(hIn) > 0) && (velForward + velRight) < maxSpeed * 2){
			if(!frictionZero){
				footL.GetComponent<Collider>().material.dynamicFriction = 0;
				footL.GetComponent<Collider>().material.staticFriction = 0;
				footR.GetComponent<Collider>().material.dynamicFriction = 0;
				footR.GetComponent<Collider>().material.staticFriction = 0;
				frictionZero = true;
			}
		} else{
			if(frictionZero){
				footL.GetComponent<Collider>().material.dynamicFriction = 1;
				footL.GetComponent<Collider>().material.staticFriction = 1;
				footR.GetComponent<Collider>().material.dynamicFriction = 1;
				footR.GetComponent<Collider>().material.staticFriction = 1;
				frictionZero = false;
			}
		}
		//			Debug.Log(rb.velocity.magnitude);
		//::Kick - If pressing walk from standstill, gives a kick so walking is more responsive.
		if(vIn > 0 && velForward < maxSpeed / 3){
			rb.AddForce(spineTF.forward * kick * 30);
			//Debug.Log("kickf"); //\\\\\\\\\\\\\\\\\\\\\\\\\\
		} else if(vIn < 0 && velForward > -maxSpeed / 3){
			rb.AddForce(spineTF.forward * -kick * 30);
			//Debug.Log("kickb"); //\\\\\\\\\\\\\\\\\\\\\\\\\\
		}
		if(hIn > 0 && velRight < maxSpeed / 3){
			rb.AddForce(spineTF.up * kick * 30);
			//Debug.Log("kickr"); //\\\\\\\\\\\\\\\\\\\\\\\\\\
		} else if(hIn < 0 && velRight > -maxSpeed / 3){
			rb.AddForce(spineTF.up * -kick * 30);
			//Debug.Log("kickl"); //\\\\\\\\\\\\\\\\\\\\\\\\\\
		}
	}

	void RigidBodyKickBack(Rigidbody rigidBody, Collision collision, ContactPoint contactPoint){
		float massMult = Mathf.Pow(2.71828f, -9 / (rigidBody.mass * rb.mass * collision.relativeVelocity.magnitude / 140));
		rigidBody.AddForceAtPosition(-(fwdVec + rightVec) * 1300 * massMult * kickBack, contactPoint.point);
	}

	void AirControl(){
		if((velForward < maxSpeed * sprint && vIn > 0) || (velForward > -maxSpeed * sprint && vIn < 0)){
			rb.velocity += spineTF.forward * airControlWalkRatio * vIn * Time.deltaTime * 10;
		}
		if((velRight < maxSpeed * sprint && hIn > 0) || (velRight > -maxSpeed * sprint && hIn < 0)){
			rb.velocity += spineTF.up * airControlWalkRatio * hIn * Time.deltaTime * 10;
		}
	}
	void Sprint(float sprintAxis, bool horizontalPressed){
		if(sprintAxis > 0){
			if(vIn > 0){
				unCrouch = isCrouching;
				//Makes guy uncrouch if isCrouching
			}
			if(vIn > 0 && sprint <= sprintSpeed && rb.velocity.magnitude > 1 && !horizontalPressed){
				sprint = Mathf.SmoothDamp(sprint, sprintSpeed, ref sprintVol1, timeToFullSprint);
			}
			else
				if((sprint <= sprintSpeed * horizSprintRatio || sprint >= sprintSpeed * horizSprintRatio + .1) && rb.velocity.magnitude > 1){
					sprint = Mathf.SmoothDamp(sprint, sprintSpeed * horizSprintRatio, ref sprintVol1, timeToFullSprint / 2);
				}
				else{
					sprint = Mathf.SmoothDamp(sprint, 1, ref sprintVol1, 0.1f);
				}
		}
		else{
			sprint = Mathf.SmoothDamp(sprint, 1, ref sprintVol1, 0.2f);
		}
	}
	void Jump(bool jumpPress, bool jumpPressed, bool jumpRelease){
		if(jumpPress && canJump){
			unCrouch = true;
			crouchBeforeJump = true;
			isJumping = true;
//				shouldJump = true;
		}
		if(crouchBeforeJump){
			if(!isReadyToJump){
				crouchAmount = Extensions.SharpInDamp(crouchAmount, jumpHold, 2.05f);
				if(jumpPressed)
					jumpHold += Time.deltaTime*7; //FIXME Needs Time.deltaTime. So do other similar things.
			}
			if(crouchAmount > .5f || jumpRelease){
				isReadyToJump = true;
				jumpHold = 0;
			}
			if(isReadyToJump){
				crouchAmount = Extensions.SharpInDamp(crouchAmount, 0f, 2.2f);
				if(crouchAmount < 0.1f){
					shouldJump = true;
					isReadyToJump = false;
					crouchBeforeJump = false;
				}
			}
		}
		if(shouldJump){
			jumpVec = (jumpHeight - (gunWeight * .07f)) * -spineTF.right * 55 * rb.mass;
			rb.AddForce(jumpVec);
			shouldJump = false;
		} else if(shouldJump && isCrouching){
			unCrouch = true;
			crouchBeforeJump = false;
		}
	}
	void JumpHolding(bool jumpPressed){
		if(isJumping && jumpPressed && jetpackFuelC > 0f){
			jetpackFuelC -= Time.deltaTime * 2.5f;
			jumpVec = (jumpHeight - (gunWeight * .07f)) * -spineTF.right * rb.mass * 280 * Time.deltaTime;
			rb.AddForce(jumpVec);
		} else{
			isJumping = false;
			jetpackFuelC = jetpackFuel;
		}
	}
	void EndJump(){
		jumpHold = 0;
		shouldJump = false;
		isReadyToJump = false;
		crouchBeforeJump = false;
	}
	void Crouch(bool crouchPress, bool crouchPressed){
		//:::Crouch	- Checks if should crouch
		if((crouchPress && crouchToggle && !isCrouching) || (crouchPressed && !crouchToggle)){
			isCrouching = true;
			crouchBeforeJump = false;
		} else if(((crouchPress && isCrouching && crouchToggle) || (!crouchToggle && !crouchPressed) || unCrouch)){
			isCrouching = false;
			unCrouch = false;
		}
		//::If should crouch, crouch; else, stand up
		if(isCrouching){
			if(crouchAmount < 1.29f)
				crouchAmount = Mathf.SmoothDamp(crouchAmount, 1.3f, ref targetScaleV, crouchSpeed); 
			if(grounded) //Not sure why this is needed. Maybe without it you would slow down in midair?
				maxSpeed = maxCrouchWalkSpeed;
		} else if(!isCrouching || unCrouch){
			if(crouchAmount > 0.01f)
				crouchAmount = Mathf.SmoothDamp(crouchAmount, 0, ref targetScaleV, crouchSpeed); 
			maxSpeed = maxWalkSpeed;
		}
	}
	void GunSwitch(bool usePress, bool usePressed, bool useRelease){
		//:::E Held/Tapped - If held, tells Gun being held to swap with gun on floor. If tapped, steals ammo.
		if(usePress){
			eTime = 0;
		}else if(usePressed){
			//While being held, increase eTime
			eTime += Time.deltaTime;
		}else if(useRelease){
			//On release, check if held for long enough, and call function accordingly
			if(eTime <= .3){
				GunSwitchCheck(false);
			}else{
				GunSwitchCheck(true);
			}
		}
	}
	void GunSwitchCheck(bool held){
		Collider[] hitColliders = Physics.OverlapSphere(transform.position, switchCheckDist, switchCheckLayerMask);
		int i = 0;
		Gun gun = null;
		while(i < hitColliders.Length){
			if(hitColliders[i].tag == "Gun Part"){
				gun = hitColliders[i].GetComponentInParent<Gun>();
			}else if(hitColliders[i].tag == "Gun"){
				gun = hitColliders[i].GetComponent<Gun>();
			}
			if(gun != null){
				if(held){
					SetGun(gun);
					StartCoroutine(gun.ChangeGun());
					gunWeight = currentGun.carryWeight + secondGun.carryWeight;
				} else{
					SetGun(gun);
					StartCoroutine(gun.LeechAmmo());
					gun.capsuleS = null;
				}
				break;
			}
			i++;
		}
	}
	internal void SetGun(Gun gun){
		gun.playerTransform = transform;
		gun.capsule = gameObject;
		gun.capsuleS = GetComponent<CapsuleScript>();
		gun.rightHand = handR;
	}

	void  Awake(){
		rb = GetComponent<Rigidbody>();
		spineTF = spine.transform;
		if(gameMode){ //Hides and Locks Cursor
			Cursor.visible = false;
			Cursor.lockState = CursorLockMode.Locked;
		}
		gunWeight = currentGun.carryWeight + secondGun.carryWeight;
		SetGun(currentGun);
		SetGun(secondGun);
		StartCoroutine(currentGun.SwitchToActive());
		StartCoroutine(secondGun.SwitchToInventory());

	}
	void  Update(){
		if(player){
			vIn = Input.GetAxis("Vertical") + playerPhysics.stepBack;
			hIn = Input.GetAxis("Horizontal") + playerPhysics.stepLeft;

			Sprint(Input.GetAxis("Sprint"), Input.GetButton("Horizontal"));
			JumpHolding(Input.GetButton("Jump"));
			Crouch(Input.GetButtonDown("Crouch"), Input.GetButton("Crouch"));
			GunSwitch(Input.GetButtonDown("Use"), Input.GetButton("Use"), Input.GetButtonUp("Use"));

			if(grounded){
				Walk();
				Jump(Input.GetButtonDown("Jump"), Input.GetButton("Jump"), Input.GetButtonUp("Jump"));
			} else{ //If not grounded:
				AirControl();
				EndJump();
				transform.position += Vector3.up * (crouchAmount - lastCrouchAmount) / 3.0f; //Move player up when crouching in air so that it's like pulling up legs.
			}
		} else{
			vIn = playerPhysics.stepBack;
			hIn = playerPhysics.stepLeft;

			Sprint(0, false);
			JumpHolding(false);
			Crouch(false, false);
			GunSwitch(false, false, false);

			if(grounded){
				Walk();
				Jump(false, false, false);
			} else{ //If not grounded:
				AirControl();
				EndJump();
				transform.position += Vector3.up * (crouchAmount - lastCrouchAmount) / 3.0f; //Move player up when crouching in air so that it's like pulling up legs.
			}
		}
		walking = (Mathf.Abs(velForward) > 0.1f || Mathf.Abs(velRight) > 0.1f);
	}
	void LateUpdate(){
		lastCrouchAmount = crouchAmount;

		//:::Level Reset
		if(Input.GetButton("L")){
			ReloadCurrentScene ();
		}
	}
	void FixedUpdate(){
		//:::Grounded - Checks if touching ground
		grounded = groundCheck.feetCheck;
		//Sets velocities if not grounded
		velForward = Vector3.Dot(rb.velocity, spineTF.forward); //Current fwd speed. Used to see if Kick is necessary.
		velRight = Vector3.Dot(rb.velocity, spineTF.up);//Similar to ^
	}


	//:::Current Velocity and Slope Calculator
	void OnCollisionStay(Collision collision){
		canJump = false;
		grounded = false;
		foreach(ContactPoint contactPoint in collision.contacts){
			if(contactPoint.thisCollider.gameObject == footL || contactPoint.thisCollider.gameObject == footR){
				currSlope = Vector3.Angle(-spineTF.right, contactPoint.normal);
				if(currSlope > maxJumpSlope || currSlope <= 0.8f || (currSlope <= 180.8f && currSlope >= 179.2f)){ //if on flat ground or in air
					tangentVec = spineTF.forward;
					tangentRightVec = spineTF.up;
				} else{
					tangentVec = Vector3.Cross(spineTF.up, contactPoint.normal);
					tangentRightVec = -Vector3.Cross(spineTF.forward, contactPoint.normal);
				}
//				Debug.DrawRay(transform.position, tangentVec, Color.blue);
				if(currSlope < maxWalkSlope){
					grounded = true;
					//Debug.Log(collision.relativeVelocity);
					velForward = Vector3.Dot(-collision.relativeVelocity, spineTF.forward); //Current fwd speed. Used to see if Kick is necessary.
					velRight = Vector3.Dot(-collision.relativeVelocity, spineTF.up);//Similar to ^
				}

				canJump |= currSlope < maxJumpSlope;

				//::KickBack
				if(collision.collider.GetComponent<Rigidbody>()){
					RigidBodyKickBack(collision.collider.GetComponent<Rigidbody>(), collision, contactPoint);
				}else if(collision.collider.gameObject.transform.parent && collision.collider.GetComponentInParent<Rigidbody>()){
					RigidBodyKickBack(collision.collider.GetComponentInParent<Rigidbody>(), collision, contactPoint);
				}
			}
		}
	}
	public void ReloadCurrentScene()
	{
		// get the current scene name 
		string sceneName = SceneManager.GetActiveScene().name;

		// load the same scene
		SceneManager.LoadScene(sceneName,LoadSceneMode.Single);
	}
}