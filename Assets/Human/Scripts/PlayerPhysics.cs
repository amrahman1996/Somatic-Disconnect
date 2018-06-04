/* TODO:
Add angle limits and ripple delay from it - Angle between two axes of body part and two axes of parent.
Add twisting with limits that doesn't ripple
Add inertia
Fix static collisions
Fix hitting reloading arm
See why leftRight is negative when hitting arms

Autodisable most SetPreRot type stuff and re-enable them when touching
Add ///Comments/// explaining purpose of blocks of code and unclear variables and such */

using System;
using System.Collections.Generic;
using ExtensionMethods;
using UnityEngine;

public class PlayerPhysics : MonoBehaviour {
    #region Variables

    public float crouchAmount;
    public HipsClass hitRotHips;
    public LegClass hitRotLegL, hitRotLegR;
    public SpineClass hitRotSpine;
    public BodyPartClass hitRotUArmL, hitRotUArmR, hitRotLArmL, hitRotLArmR;
    public GameObject headC;
    public GameObject hipsObj, hipsC;
    public GameObject spineObj, spineC, ribsC;
    public GameObject upperArmLObj, upperArmLObjC;
    public GameObject upperArmRObj, upperArmRObjC;
    public GameObject lowerArmLObj, lowerArmLObjC;
    public GameObject lowerArmRObj, lowerArmRObjC;
    public GameObject thighLObj, thighLC;
    public GameObject thighRObj, thighRC;
    public GameObject shinLObj, shinLC;
    public GameObject shinRObj, shinRC;
    public GameObject footLObj, footLObjC;
    public GameObject footRObj, footRObjC;
    public GameObject handRObj;
    public GameObject handLObj;
    public float stepBack, stepLeft;

    #endregion

    [Serializable]
    public class BodyPartClass {
        #region Variables

        private const float MaxIsTouchingPush = 300;
        public bool testMode;
        public GameObject bodyPart;
        public BodyPartClass parentPart;
        public float bodyPartStrength;
        public float massMult;
        public Vector3 positionVector, forceVector, torqueVector, tForceVector, rotVector, collisionNormal, deRot;

        private struct Extents {
            public readonly Vector3 left, top, front, center;
            public Extents(Vector3 left, Vector3 top, Vector3 front, Vector3 center) {
                this.left = left;
                this.top = top;
                this.front = front;
                this.center = center;
            }
        }

        private Extents _extents;
        public bool shouldHitRot, isTouching, isTouchingR;
        public Vector3 postForward;
        protected Quaternion preRot;
        protected Vector3 forward, up;
        private readonly Rigidbody _rb;
        private const float Mult = 25f;

        public void StoreForward(Vector3 forwardT) {
            forward = forwardT;
            if(bodyPart.name == "spine") {
                Debug.DrawRay(bodyPart.transform.TransformPoint(_extents.left), bodyPart.transform.right, Color.red);
                Debug.DrawRay(bodyPart.transform.TransformPoint(_extents.top), bodyPart.transform.up, Color.green);
                Debug.DrawRay(bodyPart.transform.TransformPoint(_extents.front), bodyPart.transform.forward, Color.blue);
            }
        }

        public void FindBounds(List<GameObject> colliderObjects) {
            Bounds bounds = new Bounds();
            foreach(GameObject co in colliderObjects) {
                Collider[] colliders = co.GetComponents<Collider>();
                foreach(Collider collider in colliders) {
                    bounds.Encapsulate(collider.bounds);
                }
            }
            _extents = new Extents(
                new Vector3(bodyPart.transform.InverseTransformPoint(bounds.max).x, 0, 0),
                new Vector3(0, bodyPart.transform.InverseTransformPoint(bounds.max).y, 0),
                new Vector3(0, 0, bodyPart.transform.InverseTransformPoint(bounds.max).z),
                bodyPart.transform.InverseTransformPoint(bounds.center)
            );
        }

        #region Constructors

        public BodyPartClass(GameObject bodyPart) {
            this.bodyPart = bodyPart;
            _rb = bodyPart.GetComponentInParent<Rigidbody>();
            bodyPartStrength = 0.5f;
        }

        public BodyPartClass(GameObject bodyPart, float bodyPartStrength) {
            this.bodyPart = bodyPart;
            _rb = bodyPart.GetComponentInParent<Rigidbody>();
            this.bodyPartStrength = bodyPartStrength;
        }

        public void AddParent(BodyPartClass parent) {
            parentPart = parent;
        }

        #endregion

        #endregion

        protected Vector3 GetOctant() {
            float upDown = Mathf.Asin(Vector3.Dot(_extents.center - bodyPart.transform.position - positionVector, bodyPart.transform.right));
            float leftRight = Mathf.Asin(-Vector3.Dot(positionVector, bodyPart.transform.up));
            float frontBack = Mathf.Asin(Vector3.Dot(positionVector, bodyPart.transform.forward));
            Vector3 info = new Vector3(upDown, leftRight, frontBack);
            if(bodyPart.name == "spine") {
                Debug.Log("upDown: " + upDown + " : " + "frontBack: " + frontBack + " : " + "leftRight: " + leftRight
                        + "dot: " + Vector3.Dot(_extents.center - bodyPart.transform.position - positionVector, bodyPart.transform.right));
            }

            return info;
        }

        public Vector3 GetParentAngles() {
            Vector3 upDownPlaneNormal = Vector3.Cross(parentPart.forward, forward);
            float upDown = Vector3.Angle(Vector3.ProjectOnPlane(-parentPart.forward, upDownPlaneNormal),
                Vector3.ProjectOnPlane(forward, upDownPlaneNormal));
            float leftRight = Mathf.Rad2Deg * Mathf.Acos(-Vector3.Dot(positionVector, bodyPart.transform.up));
            float frontBack = Mathf.Rad2Deg * Mathf.Acos(Vector3.Dot(positionVector, bodyPart.transform.forward));
            Vector3 info = new Vector3(upDown, leftRight, frontBack);
//			Debug.Log("upDown: " + upDown + " : " + "frontBack: " + frontBack + " : " + "leftRight: " + leftRight);
            return info;
        }

        /// <summary> Calculate how much rotation should be added on collision. </summary>
        /// <param name="point">Point of contact</param>
        /// <param name="direction">Direction of contact</param>
        /// <param name="rVelocity">Relative Velocity of Collision</param>
        /// <param name="mass">Mass of colliding object</param>
        public virtual void HitCalc(Vector3 point, Vector3 direction, Vector3 rVelocity, float mass) {
            shouldHitRot = true; //enable HitRot() to apply rotation
            isTouching = true; //enable IsTouching() for continuous touching
            collisionNormal = direction;
            positionVector = (point - bodyPart.transform.position).normalized; //TODO why is this normalized?
            //Determines how much influence collision will have. Ranges from 0 to 1.
            massMult = isTouchingR ? Mathf.Exp(-9 / (mass * rVelocity.magnitude / bodyPartStrength + 2f))
                           : Mathf.Exp(9 / (mass / bodyPartStrength + 2f));

//			Debug.DrawRay(bodyPart.transform.position, direction.normalized, Color.green);
//			Debug.DrawRay(bodyPart.transform.position, rVelocity.normalized, Color.red);
//			Debug.Log(Vector3.Dot(direction.normalized, rVelocity.normalized) + " : " + Time.fixedTime);

            if(!(Vector3.Dot(direction.normalized, rVelocity.normalized) > 0.01f))
                return; //Makes sure an object sliding away doesn't cause errant rotations
            forceVector = Vector3.Project(rVelocity, direction);

            //a vector perpendicular to the normal force and the bodyParts forward vector to use during rotation. This is torque = r X F
            Vector3 torquePlus = massMult * Vector3.Cross(positionVector, forceVector);
            GetOctant();
            AddTorque(point, rVelocity, mass, torquePlus);
//				Debug.Log(hitRotDot + " : " + torquePlus.sqrMagnitude);
        }

        protected void AddTorque(Vector3 point, Vector3 rVelocity, float mass, Vector3 torquePlus) {
            if(!(Vector3.Dot(torquePlus, rotVector) < torquePlus.sqrMagnitude * 0.8f))
                return; //if torquePlus is too close to what rotVector already is

            torqueVector += torquePlus; //FIXME should include Time.fixedDeltaTime

            //force that is parallel to limb or too close to hinge, so can't rotate it, but can be transferred to parent. 	
            tForceVector = -0.2f * (rVelocity - forceVector) - collisionNormal * (forceVector.magnitude - Vector3.Cross(positionVector, forceVector).magnitude);
            if(parentPart != null) {
                parentPart.HitTransfer(bodyPart.transform.position, rVelocity, collisionNormal, tForceVector, mass);
            }
        }

        public virtual void HitTransfer(Vector3 point, Vector3 rVelocity, Vector3 direction, Vector3 transferredForceVector, float mass) {
            shouldHitRot = true;
            massMult = Mathf.Exp(-9 / (mass / bodyPartStrength + 2f));
            positionVector = bodyPart.transform.position - point;
            Vector3 torquePlus = massMult * Vector3.Cross(positionVector, transferredForceVector); //is this problem?
            Vector3 unknownVel = Mathf.Abs(rVelocity.magnitude - _rb.velocity.magnitude) * rVelocity.normalized;
            AddTorque(point, unknownVel, mass, torquePlus);
        }

        public virtual void HitRot() { //Then apply the rotations calculated
            if(!shouldHitRot && !testMode) return;

            Vector3 localTorquVector = bodyPart.transform.InverseTransformDirection(torqueVector + deRot);
            rotVector += localTorquVector * Time.deltaTime;
            deRot -= deRot * 10 * Time.deltaTime;
            torqueVector -= torqueVector * 5 * Time.deltaTime;
            bodyPart.transform.Rotate(Mult * rotVector, Space.Self);
            if(!testMode) {
                rotVector = Extensions.SharpInDamp(rotVector, rotVector / 2, 0.8f);
            }

            shouldHitRot = rotVector.sqrMagnitude * Mult * Mult >= 0.01f;
        }

        public virtual void IsTouching(float posNeg = 1, float posNegParent = 1) { //adjust rotation when rotating into something
            if(!isTouching || !(Vector3.Dot(collisionNormal, posNeg * (postForward - forward).normalized) > 0.1f)) return;

            Vector3 torquePlus = posNeg * 30 * Mult * massMult * positionVector.magnitude * Vector3.Angle(postForward, forward) *
                                 Vector3.Cross(collisionNormal, postForward);

            if(!(Vector3.Dot(torquePlus, torqueVector) < torquePlus.sqrMagnitude * 0.8f) ||
               !(torquePlus.sqrMagnitude / 10000 < MaxIsTouchingPush)) return; //torquePlus is too close to what rotVector already is

            deRot += torquePlus * Time.deltaTime;
            shouldHitRot = true;
            if(parentPart != null) {
                parentPart.deRot += posNegParent * torquePlus * Time.deltaTime;
                parentPart.shouldHitRot = true;
            }
        }

        public void TouchReset() {
            isTouchingR = false;
            isTouching = false;
        }
    }

    [Serializable]
    public class SpineClass : BodyPartClass {
        #region Variables

        private BodyPartClass _armL, _armR;
        public float twistPlus, twistRot, tV;

        #region Constructors

        public SpineClass(GameObject bodyPart) : base(bodyPart) {
            bodyPartStrength = 0.5f;
        }

        public SpineClass(GameObject bodyPart, float bodyPartStrength) : base(bodyPart, bodyPartStrength) { }

        public void addChildren(BodyPartClass armL, BodyPartClass armR) {
            _armL = armL;
            _armR = armR;
        }

        #endregion

        public float rotTest;

        #endregion

        private void TwistCalc(Vector3 rVelocity) {
            Vector3 projPosVec = Vector3.ProjectOnPlane(positionVector.normalized, bodyPart.transform.up);
            Vector3 twistVec = Vector3.Cross(-rVelocity, projPosVec);
            float twistSign = Vector3.Dot(twistVec, bodyPart.transform.up) / Mathf.Abs(Vector3.Dot(twistVec, bodyPart.transform.up));
            float twist = 500 * massMult * twistVec.magnitude * torqueVector.magnitude * twistSign; //TODO Make based on torqueVector or something as well
//			Debug.Log(twist);
            twistPlus += twist * Time.fixedDeltaTime;
        }

        /// <inheritdoc />
        public override void HitCalc(Vector3 point, Vector3 direction, Vector3 rVelocity, float mass) {
            shouldHitRot = true;
            isTouching = true;
            collisionNormal = direction;

            massMult = Mathf.Exp(9 / (mass / bodyPartStrength + 2f)); //how much influence collision will have.

//			Debug.DrawRay(bodyPart.transform.position, direction.normalized, Color.green);
//			Debug.DrawRay(bodyPart.transform.position, rVelocity.normalized, Color.red);

            if(!(Vector3.Dot(direction.normalized, rVelocity.normalized) > 0.05f))
                return; //Makes sure an object sliding away doesn't cause errant rotations

            positionVector = point - bodyPart.transform.position;
//				Debug.Log(positionVector.magnitude);
            forceVector = Vector3.Project(rVelocity, direction);
            //a vector perpendicular to the normal force and the bodyParts forward vector to use during rotation. This is torque = r X F

            Vector3 torquePlus = massMult * Vector3.Cross(positionVector, forceVector);
            //TODO TODO TODO Figure out how/when to make it a hips hitCalc instead.
            GetOctant();
            if(Vector3.Dot(torquePlus, rotVector) < torquePlus.sqrMagnitude * 0.8f) {
                //if torquePlus isn't too close to what rotVector already is:
                torqueVector += torquePlus; //FIXME Probably should include Time.fixedDeltaTime
                _armL.torqueVector -= torquePlus;
                _armR.torqueVector -= torquePlus;
                tForceVector = -forceVector; //force that is parallel to limb or too close to hinge, so can't rotate it, but can be transferred to parent.
                parentPart.HitTransfer(point + (point - bodyPart.transform.position).normalized * .5f, rVelocity, direction, tForceVector, mass);
                TwistCalc(rVelocity);
            }

//            if(Vector3.Dot(direction.normalized, Vector3.down) > 0.05f) {
//                Vector3 gravityForce = 1 * Vector3.Dot(direction, Vector3.down) * Vector3.down;
//                Debug.Log("Gravity " + Vector3.Dot(direction.normalized, Vector3.down));
            //a vector perpendicular to the normal force and the bodyParts forward vector to use during rotation. This is torque = r X F
//                torqueVector += massMult * Vector3.Cross(positionVector, gravityForce);
//            }
        }

        public override void HitTransfer(Vector3 point, Vector3 rVelocity, Vector3 direction, Vector3 transferredForceVector, float mass) {
//            shouldHitRot = true;
//            massMult = Mathf.Exp(-9 / (mass / bodyPartStrength + 2f));
//            positionVector = -(point - bodyPart.transform.position);
////			Debug.Log(positionVector.magnitude);
//            Vector3 torquePlus = massMult * Vector3.Cross(positionVector, transferredForceVector); //is this problem?
//            float hitRotDot = Vector3.Dot(torquePlus, rotVector);
//            GetOctant();
//            if(hitRotDot < torquePlus.sqrMagnitude * 0.8f) { //if torquePlus isn't too close to what rotVector already is: //TODO Can I use AddTorque here and/or in HitCalc?
//                torqueVector += torquePlus;
//                tForceVector = -0.2f * (rVelocity - transferredForceVector) -
//                               direction * (transferredForceVector.magnitude - Vector3.Cross(positionVector, transferredForceVector).magnitude);
//                if(parentPart != null) {
//                    parentPart.HitTransfer(point + (point - bodyPart.transform.position).normalized * .5f, rVelocity, direction, tForceVector, mass);
//                }
//            }
//            TwistCalc(-rVelocity);
        }

        public override void HitRot() {
            base.HitRot();
            twistRot += (twistPlus + rotTest) * Time.deltaTime;
            if(Mathf.Abs(twistRot) > 0.01f) {
                bodyPart.transform.Rotate(bodyPart.transform.up * twistRot, Space.World);
                twistPlus -= twistPlus * 5 * Time.deltaTime;
                twistRot = Mathf.SmoothDamp(twistRot, twistRot / 2, ref tV, 0.2f);
            } else {
                twistPlus = 0;
            }
        }
    }

    [Serializable]
    public class LegClass : BodyPartClass {
        #region Variables

        public readonly GameObject thigh, shin, foot;
        public float stepFrontAmount, stepSideAmount;
        private const float Mult = 12.5f;
        private Vector3 rV;
        private float cV, cVV;

        #region Constructors

        public LegClass(GameObject thigh, GameObject shin, GameObject foot) : base(thigh) {
            this.thigh = thigh;
            this.shin = shin;
            this.foot = foot;
            bodyPartStrength = 0.5f;
        }

        public LegClass(GameObject thigh, GameObject shin, GameObject foot, float bodyPartStrength) : base(thigh, bodyPartStrength) {
            this.thigh = thigh;
            this.shin = shin;
            this.foot = foot;
        }

        #endregion

        #endregion

        public void HitCalcShin(Vector3 point, Vector3 direction, Vector3 rVelocity, float mass, ref float crouchAmountP) {
            Vector3 upVector = Vector3.Project(rVelocity, Vector3.up);
            massMult = Mathf.Pow(2.71828f,
                -9 / (mass / bodyPartStrength + 2f)); //Determines how much influence collision will have. Ranges from 0 to 1.
            float cASet = upVector.magnitude / 6 * massMult;
            if(Vector3.Dot(rVelocity, Vector3.up) > 0.01f && cASet > crouchAmountP)
                crouchAmountP = cASet; //Makes guy crouch from impact
            forceVector = Vector3.Project(rVelocity, direction);
            Vector3 mForceVector = Vector3.Dot(upVector, Vector3.up) > 0f ? forceVector - upVector : forceVector;
            if(mForceVector.magnitude > 0.01f) {
//				Debug.Log(mForceVector.magnitude);
                shouldHitRot = true;
                isTouching = true;
                collisionNormal = direction;
//				Debug.DrawRay(bodyPart.transform.position, direction.normalized, Color.green);
//				Debug.DrawRay(bodyPart.transform.position, rVelocity.normalized, Color.red);
//				Debug.Log(Vector3.Dot(direction.normalized, rVelocity.normalized) + " : " + Time.fixedTime);

                if(!(Vector3.Dot(direction.normalized, rVelocity.normalized) > 0.05f))
                    return; //Makes sure an object sliding away doesn't cause errant rotations

                positionVector = (point - bodyPart.transform.position).normalized;
                //a vector perpendicular to the normal force and the bodyParts forward vector to use during rotation.
                Vector3 torquePlus = massMult * Vector3.Cross(positionVector, mForceVector);
                float hitRotDot = Vector3.Dot(torquePlus, rotVector);
                GetOctant();
                if(hitRotDot < torquePlus.sqrMagnitude * 0.8f) { //if torquePlus isn't too close to what rotVector already is:
                    torqueVector += torquePlus;
                    //force that is parallel to limb or too close to hinge, so can't rotate it, but can be transferred to parent.
                    tForceVector = -0.2f * (rVelocity - mForceVector) - direction *
                                   (mForceVector.magnitude - Vector3.Cross(positionVector, mForceVector).magnitude);
                }

//					Debug.Log(hitRotDot + " : " + torquePlus.sqrMagnitude);
            }
        }

        public override void HitRot() {
            if(!shouldHitRot && !testMode) return;

            Vector3 localTorqueVector = shin.transform.InverseTransformDirection(torqueVector + deRot);
            rotVector += localTorqueVector * Time.deltaTime;
            deRot -= deRot * 10 * Time.deltaTime;
            torqueVector -= torqueVector * 5 * Time.deltaTime;
            thigh.transform.Rotate(Mult * rotVector, Space.Self);
            shin.transform.Rotate(Mult * rotVector, Space.Self); //TODO Separate shin rot from thigh rot in some way
            //foot.transform.Rotate(mult * rotVector, Space.World);
            if(!testMode)
                rotVector = Vector3.SmoothDamp(rotVector, rotVector / 2, ref rV, 0.2f);
            shouldHitRot = rotVector.sqrMagnitude * Mult * Mult >= 0.01f;
        }

        public override void HitTransfer(Vector3 point, Vector3 rVelocity, Vector3 direction, Vector3 transferredForceVector, float mass) {
            shouldHitRot = true;
            massMult = Mathf.Exp(-9 / (mass / bodyPartStrength + 2f));
            positionVector = -(point - bodyPart.transform.position);
            Vector3 torquePlus = massMult * Vector3.Cross(positionVector, transferredForceVector); //is this problem?
            AddTorque(point, rVelocity, mass, torquePlus);
        }

        public override void IsTouching(float posNeg = 1, float posNegParent = 1) {
            if(!isTouching || !(Vector3.Dot(collisionNormal, Vector3.up) < 0.2f) ||
               !(Vector3.Dot(collisionNormal, posNeg * (postForward - forward).normalized) > 0.1f)) return;

            Vector3 torquePlus = posNeg * Mult * massMult * positionVector.magnitude * Vector3.Angle(postForward, forward) *
                                 Vector3.Cross(collisionNormal, postForward);

            if(!(Vector3.Dot(torquePlus, rotVector) < torquePlus.sqrMagnitude * 0.8f))
                return; //if torquePlus is too close to what rotVector already is
            deRot += torquePlus * Time.deltaTime;
            shouldHitRot = true;
        }

        public void LegControl(float stepFront, float stepSide, float crouch) {
            thigh.transform.Rotate(thigh.transform.right, -stepFront * 0.7f, Space.World);
            thigh.transform.Rotate(thigh.transform.right, -crouch * 0.4f, Space.World); //Try something with slerp
            shin.transform.Rotate(shin.transform.right, stepFront * 0.3f, Space.World);
            shin.transform.Rotate(shin.transform.right, crouch * 0.8f, Space.World);
            foot.transform.Rotate(foot.transform.right, stepFront * 0.5f, Space.World);
            foot.transform.Rotate(foot.transform.right, -crouch * 0.4f, Space.World);
        }
    }

    [Serializable]
    public class HipsClass : BodyPartClass {
        #region Variables

        private LegClass legL, legR;
        private BodyPartClass spine;
        private readonly GameObject man;
        private Vector3 rV;

        #region Constructors

        public HipsClass(GameObject bodyPart, GameObject man) : base(bodyPart) {
            this.man = man;
            bodyPartStrength = 0.5f;
        }

        public HipsClass(GameObject bodyPart, GameObject man, float bodyPartStrength) : base(bodyPart, bodyPartStrength) {
            this.man = man;
        }

        public void AddChildren(BodyPartClass spine, LegClass legL, LegClass legR) {
            this.spine = spine;
            this.legL = legL;
            this.legR = legR;
        }

        #endregion

        #endregion

        public override void HitCalc(Vector3 point, Vector3 direction, Vector3 rVelocity, float mass) {
            //Calculate how much rotation should continue and be added on continued collision
            shouldHitRot = true;
            isTouching = true;
            collisionNormal = direction;
            massMult = Mathf.Pow(2.71828f,
                -9 / (mass / bodyPartStrength + 2f)); //Determines how much influence collision will have. Ranges from 0 to 1.
            if(Vector3.Dot(direction.normalized, rVelocity.normalized) > 0.05f) { //Makes sure an object sliding away doesn't cause errant rotations
                forceVector = Vector3.Project(rVelocity, direction);
                spine.HitCalc(point, direction, forceVector, mass);
                legL.HitCalc(point, direction, forceVector, mass);
                legR.HitCalc(point, direction, forceVector, mass);
                positionVector = (point - bodyPart.transform.position).normalized;
                //a vector perpendicular to the normal force and the bodyParts forward vector to use during rotation. This is torque = r X F
                Vector3 torquePlus = massMult * Vector3.Cross(positionVector, forceVector);
                float hitRotDot = Vector3.Dot(torquePlus, rotVector);
                GetOctant();
                if(hitRotDot < torquePlus.sqrMagnitude * 0.8f) { //if torquePlus isn't too close to what rotVector already is:
                    torqueVector += torquePlus; //FIXME Probably should include Time.fixedDeltaTime
                    tForceVector = -0.2f * (rVelocity - forceVector) -
                                   direction * (forceVector.magnitude -
                                                Vector3.Cross(positionVector, forceVector).magnitude
                                               ); //force that is parallel to limb or too close to hinge, so can't rotate it, but can be transferred to parent. 	
                }
            }
        }

        public void HitRot(ref float stepBack, ref float stepLeft) {
            if(shouldHitRot || testMode) {
                rotVector += torqueVector * Time.deltaTime;
                torqueVector = Vector3.zero;
//				Debug.DrawRay(bodyPart.transform.position, bodyPart.transform.forward, Color.blue);
//				Debug.DrawRay(bodyPart.transform.position, bodyPart.transform.up, Color.red);
//				Debug.DrawRay(bodyPart.transform.position, rotVector.normalized, Color.white);
                man.transform.rotation *= Quaternion.AngleAxis(-25 * rotVector.magnitude, rotVector);
                if(isTouchingR || !isTouching && !spine.isTouching) { //TODO Make step only after certain amount of offness from feet
                    float sB = Vector3.Dot(rotVector, bodyPart.transform.right);
                    float sL = -Vector3.Dot(rotVector, bodyPart.transform.forward);
//					if(Mathf.Abs(sB) > 0.07f)
//						stepBack = sB;
//					if(Mathf.Abs(sL) > 0.07f)
//						stepLeft = sL;
                } else {
                    stepBack = 0;
                    stepLeft = 0;
                }

                if(!testMode)
                    rotVector = Vector3.SmoothDamp(rotVector, Vector3.zero, ref rV, 0.2f);
                shouldHitRot = rotVector.sqrMagnitude * 125 >= 0.01f;
            } else {
                stepBack = 0;
                stepLeft = 0;
            }
        }

        public void Balance() {
            Vector3 footAvg = (legL.foot.transform.position + legR.foot.transform.position) / 2;
            Vector3 balanceVec = (bodyPart.transform.position - footAvg).normalized;
            float forwardDot = Vector3.Dot(balanceVec, Vector3.ProjectOnPlane(bodyPart.transform.forward, Vector3.up));
//			Debug.Log(forwardDot);
            if(Mathf.Abs(forwardDot) > .2) {
                legL.shouldHitRot = true;
                legL.rotVector += bodyPart.transform.right * forwardDot; //FIXME need to change animation target rather than rotVector
                legR.shouldHitRot = true;
                legR.rotVector += bodyPart.transform.right * forwardDot;
            }
        }
    }

    #region CollisionsNThings

    private void Awake() {
        hitRotHips = new HipsClass(hipsObj, gameObject, 0.4f);
        hitRotLegL = new LegClass(thighLObj, shinLObj, footLObj, 0.35f);
        hitRotLegR = new LegClass(thighRObj, shinRObj, footRObj, 0.35f);
        hitRotSpine = new SpineClass(spineObj, 0.5f);
        hitRotLArmL = new BodyPartClass(lowerArmLObj, 0.2f);
        hitRotLArmR = new BodyPartClass(lowerArmRObj, 0.2f);
        hitRotUArmL = new BodyPartClass(upperArmLObj, 0.2f);
        hitRotUArmR = new BodyPartClass(upperArmRObj, 0.2f);
        hitRotHips.AddChildren(hitRotSpine, hitRotLegL, hitRotLegR);
        hitRotLegL.AddParent(hitRotHips);
        hitRotLegR.AddParent(hitRotHips);
        hitRotSpine.AddParent(hitRotHips);
        hitRotSpine.addChildren(hitRotUArmL, hitRotUArmR);
        hitRotUArmL.AddParent(hitRotSpine);
        hitRotUArmR.AddParent(hitRotSpine);
        hitRotLArmL.AddParent(hitRotUArmL);
        hitRotLArmR.AddParent(hitRotUArmR);
        hitRotLegL.FindBounds(new List<GameObject> {thighLC, footLObjC, shinLC});
        hitRotLegR.FindBounds(new List<GameObject> {thighRC, footRObjC, shinRC});
        hitRotSpine.FindBounds(new List<GameObject> {spineC, ribsC, headC});
        hitRotHips.FindBounds(new List<GameObject> {hipsC});
        hitRotUArmL.FindBounds(new List<GameObject> {upperArmLObjC});
        hitRotUArmR.FindBounds(new List<GameObject> {upperArmRObjC});
        hitRotLArmL.FindBounds(new List<GameObject> {lowerArmLObjC, handLObj});
        hitRotLArmR.FindBounds(new List<GameObject> {lowerArmRObjC, handRObj});
    }

    private void OnCollisionEnter(Collision collisionInfo) {
        if(collisionInfo.gameObject.GetComponent<Rigidbody>()) {
            foreach(ContactPoint contact in collisionInfo.contacts) {
                if(contact.thisCollider.gameObject == shinLC || contact.thisCollider.gameObject == footLObjC) {
                    hitRotLegL.isTouchingR = true;
                    hitRotLegL.HitCalcShin(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass, ref crouchAmountP);
                } else if(contact.thisCollider.gameObject == shinRC || contact.thisCollider.gameObject == footRObjC) {
                    hitRotLegR.isTouchingR = true;
                    hitRotLegR.HitCalcShin(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass, ref crouchAmountP);
                } else if(contact.thisCollider.gameObject == spineC || contact.thisCollider.gameObject == headC ||
                          contact.thisCollider.gameObject == ribsC) { //Put in order of likeleness to be hit for optimization
                    hitRotSpine.isTouchingR = true;
                    hitRotSpine.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass);
                } else if(contact.thisCollider.gameObject == hipsC) { //Put in order of likeleness to be hit for optimization
                    hitRotHips.isTouchingR = true;
                    hitRotHips.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass);
                } else if(contact.thisCollider.gameObject == lowerArmLObjC || contact.thisCollider.gameObject == handLObj) {
                    hitRotLArmL.isTouchingR = true;
                    hitRotLArmL.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass);
                } else if(contact.thisCollider.gameObject == lowerArmRObjC || contact.thisCollider.gameObject == handRObj ||
                          contact.thisCollider.GetComponentInParent<Gun>()) {
                    hitRotLArmR.isTouchingR = true;
                    hitRotLArmR.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass);
                } else if(contact.thisCollider.gameObject == upperArmLObjC) {
                    hitRotUArmL.isTouchingR = true;
                    hitRotUArmL.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass);
                } else if(contact.thisCollider.gameObject == upperArmRObjC) {
                    hitRotUArmR.isTouchingR = true;
                    hitRotUArmR.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass);
                } else if(contact.thisCollider.gameObject == thighLC) {
                    hitRotLegL.isTouchingR = true;
                    hitRotLegL.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass);
                } else if(contact.thisCollider.gameObject == thighRC) {
                    hitRotLegR.isTouchingR = true;
                    hitRotLegR.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass);
                }
            }
        } else {
            foreach(ContactPoint contact in collisionInfo.contacts) {
                if(contact.thisCollider.gameObject == shinLC || contact.thisCollider.gameObject == footLObjC) {
                    hitRotLegL.HitCalcShin(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000, ref crouchAmountP);
                } else if(contact.thisCollider.gameObject == thighRC || contact.thisCollider.gameObject == shinRC ||
                          contact.thisCollider.gameObject == footRObjC) {
                    hitRotLegR.HitCalcShin(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000, ref crouchAmountP);
                } else if(contact.thisCollider.gameObject == spineC || contact.thisCollider.gameObject == headC ||
                          contact.thisCollider.gameObject == ribsC) { //Put in order of likeleness to be hit for optimization
                    hitRotSpine.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000);
                } else if(contact.thisCollider.gameObject == hipsC) { //Put in order of likeleness to be hit for optimization
                    hitRotHips.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000);
                } else if(contact.thisCollider.gameObject == lowerArmLObjC || contact.thisCollider.gameObject == handLObj) {
                    hitRotLArmL.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000);
                } else if(contact.thisCollider.gameObject == lowerArmRObjC || contact.thisCollider.gameObject == handRObj ||
                          contact.thisCollider.GetComponentInParent<Gun>()) {
                    hitRotLArmR.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000);
                } else if(contact.thisCollider.gameObject == upperArmLObjC) {
                    hitRotUArmL.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000);
                } else if(contact.thisCollider.gameObject == upperArmRObjC) {
                    hitRotUArmR.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000);
                } else if(contact.thisCollider.gameObject == thighLC) {
                    hitRotLegL.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000);
                } else if(contact.thisCollider.gameObject == thighRC) {
                    hitRotLegR.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000);
                }
            }
        }
    }

    private void OnCollisionStay(Collision collisionInfo) {
        if(collisionInfo.gameObject.GetComponent<Rigidbody>()) {
            foreach(ContactPoint contact in collisionInfo.contacts) {
                if(contact.thisCollider.gameObject == shinLC || contact.thisCollider.gameObject == footLObjC) {
                    hitRotLegL.isTouchingR = true;
                    hitRotLegL.HitCalcShin(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass, ref crouchAmountP);
                } else if(contact.thisCollider.gameObject == thighRC || contact.thisCollider.gameObject == shinRC ||
                          contact.thisCollider.gameObject == footRObjC) {
                    hitRotLegR.isTouchingR = true;
                    hitRotLegR.HitCalcShin(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass, ref crouchAmountP);
                } else if(contact.thisCollider.gameObject == spineC || contact.thisCollider.gameObject == headC ||
                          contact.thisCollider.gameObject == ribsC) { //Put in order of likeleness to be hit for optimization
                    hitRotSpine.isTouchingR = true;
                    hitRotSpine.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass);
                } else if(contact.thisCollider.gameObject == hipsC) { //Put in order of likeleness to be hit for optimization
                    hitRotHips.isTouchingR = true;
                    hitRotHips.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass);
                } else if(contact.thisCollider.gameObject == lowerArmLObjC || contact.thisCollider.gameObject == handLObj) {
                    hitRotLArmL.isTouchingR = true;
                    hitRotLArmL.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass);
                } else if(contact.thisCollider.gameObject == lowerArmRObjC || contact.thisCollider.gameObject == handRObj ||
                          contact.thisCollider.GetComponentInParent<Gun>()) {
                    hitRotLArmR.isTouchingR = true;
                    hitRotLArmR.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass);
                } else if(contact.thisCollider.gameObject == upperArmLObjC) {
                    hitRotUArmL.isTouchingR = true;
                    hitRotUArmL.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass);
                } else if(contact.thisCollider.gameObject == upperArmRObjC) {
                    hitRotUArmR.isTouchingR = true;
                    hitRotUArmR.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass);
                } else if(contact.thisCollider.gameObject == thighLC) {
                    hitRotLegL.isTouchingR = true;
                    hitRotLegL.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass);
                } else if(contact.thisCollider.gameObject == thighRC) {
                    hitRotLegR.isTouchingR = true;
                    hitRotLegR.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity,
                        collisionInfo.gameObject.GetComponent<Rigidbody>().mass);
                }
            }
        } else {
            foreach(ContactPoint contact in collisionInfo.contacts) {
                if(contact.thisCollider.gameObject == shinLC || contact.thisCollider.gameObject == footLObjC) {
                    hitRotLegL.HitCalcShin(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000, ref crouchAmountP);
                } else if(contact.thisCollider.gameObject == thighRC || contact.thisCollider.gameObject == shinRC ||
                          contact.thisCollider.gameObject == footRObjC) {
                    hitRotLegR.HitCalcShin(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000, ref crouchAmountP);
                } else if(contact.thisCollider.gameObject == spineC || contact.thisCollider.gameObject == headC ||
                          contact.thisCollider.gameObject == ribsC) { //Put in order of likeleness to be hit for optimization
                    hitRotSpine.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000);
                } else if(contact.thisCollider.gameObject == hipsC) { //Put in order of likeleness to be hit for optimization
                    hitRotHips.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000);
                } else if(contact.thisCollider.gameObject == lowerArmLObjC || contact.thisCollider.gameObject == handLObj) {
                    hitRotLArmL.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000);
                } else if(contact.thisCollider.gameObject == lowerArmRObjC || contact.thisCollider.gameObject == handRObj ||
                          contact.thisCollider.GetComponentInParent<Gun>()) {
                    hitRotLArmR.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000);
                } else if(contact.thisCollider.gameObject == upperArmLObjC) {
                    hitRotUArmL.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000);
                } else if(contact.thisCollider.gameObject == upperArmRObjC) {
                    hitRotUArmR.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000);
                } else if(contact.thisCollider.gameObject == thighLC) {
                    hitRotLegL.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000);
                } else if(contact.thisCollider.gameObject == thighRC) {
                    hitRotLegR.HitCalc(contact.point, contact.normal, collisionInfo.relativeVelocity, 1000);
                }
            }
        }
    }

    public void OnBullet(object[] hitInfo) {
        RaycastHit hit = (RaycastHit) hitInfo[0];
        Vector3 force = (Vector3) hitInfo[1];
        const float mass = 1.2f;
//		Debug.DrawRay(hit.point, -hit.normal);
//		Debug.DrawRay(hit.point, force, Color.green);
//		Debug.Log(Vector3.Dot(-hit.normal, force.normalized));
//		Debug.Log(hit.collider.gameObject);
        if(hit.collider.gameObject == shinLC || hit.collider.gameObject == footLObjC) {
            hitRotLegL.HitCalcShin(hit.point, -hit.normal, force, mass, ref crouchAmountP);
        } else if(hit.collider.gameObject == thighRC || hit.collider.gameObject == shinRC || hit.collider.gameObject == footRObjC) {
            hitRotLegR.HitCalcShin(hit.point, -hit.normal, force, mass, ref crouchAmountP);
        } else if(hit.collider.gameObject == spineC || hit.collider.gameObject == headC || hit.collider.gameObject == ribsC) {
            //Put in order of likeleness to be hit for optimization
            hitRotSpine.HitCalc(hit.point, -hit.normal, force, mass);
        } else if(hit.collider.gameObject == hipsC) { //Put in order of likeleness to be hit for optimization
            hitRotHips.HitCalc(hit.point, -hit.normal, force, mass);
            hitRotHips.isTouchingR = true;
        } else if(hit.collider.gameObject == lowerArmLObjC || hit.collider.gameObject == handLObj) {
            hitRotLArmL.HitCalc(hit.point, -hit.normal, force, mass);
        } else if(hit.collider.gameObject == lowerArmRObjC || hit.collider.gameObject == handRObj || hit.collider.GetComponentInParent<Gun>()) {
            hitRotLArmR.HitCalc(hit.point, -hit.normal, force, mass);
        } else if(hit.collider.gameObject == upperArmLObjC) {
            hitRotUArmL.HitCalc(hit.point, -hit.normal, force, mass);
        } else if(hit.collider.gameObject == upperArmRObjC) {
            hitRotUArmR.HitCalc(hit.point, -hit.normal, force, mass);
        } else if(hit.collider.gameObject == thighLC) {
            hitRotLegL.HitCalc(hit.point, -hit.normal, force, mass);
        } else if(hit.collider.gameObject == thighRC) {
            hitRotLegR.HitCalc(hit.point, -hit.normal, force, mass);
        }
    }

    private void FixedUpdate() {
        hitRotLegL.TouchReset();
        hitRotLegR.TouchReset();
        hitRotHips.TouchReset();
        hitRotSpine.TouchReset();
        hitRotUArmL.TouchReset();
        hitRotUArmR.TouchReset();
        hitRotLArmL.TouchReset();
        hitRotLArmR.TouchReset();
    }

    public float crouchAmountP, crouchAmountSmooth;

    private void Update() {
        crouchAmountSmooth = Extensions.SharpInDamp(crouchAmountSmooth, crouchAmountP, 3.0f);
        crouchAmountP -= crouchAmountP * Time.deltaTime * 2;
        crouchAmount = crouchAmountSmooth;
        hitRotLegL.StoreForward(thighLObj.transform.forward);
        hitRotLegR.StoreForward(thighRObj.transform.forward);
        hitRotSpine.StoreForward(spineObj.transform.forward);
        hitRotHips.StoreForward(hipsObj.transform.forward);
        hitRotUArmL.StoreForward(upperArmLObj.transform.forward);
        hitRotUArmR.StoreForward(upperArmRObj.transform.forward);
        hitRotLArmL.StoreForward(lowerArmLObj.transform.forward);
        hitRotLArmR.StoreForward(-lowerArmRObj.transform.forward);


        hitRotLArmR.GetParentAngles();
    }

    private void LateUpdate() {
        hitRotLegL.HitRot();
//		hitRotLegL.LegControl(xxx, xxx, xxx1);
        hitRotLegR.HitRot();
//		hitRotLegR.LegControl(xxx, xxx, xxx1);
        hitRotSpine.HitRot();
        hitRotHips.HitRot(ref stepBack, ref stepLeft);
        hitRotUArmL.HitRot();
        hitRotUArmR.HitRot();
        hitRotLArmL.HitRot();
        hitRotLArmR.HitRot();
        hitRotLegL.postForward = thighLObj.transform.forward;
        hitRotLegR.postForward = thighRObj.transform.forward;
        hitRotSpine.postForward = spineObj.transform.forward;
        hitRotHips.postForward = hipsObj.transform.forward;
        hitRotUArmL.postForward = upperArmLObj.transform.forward;
        hitRotUArmR.postForward = upperArmRObj.transform.forward;
        hitRotLArmL.postForward = lowerArmLObj.transform.forward;
        hitRotLArmR.postForward = -lowerArmRObj.transform.forward;
        hitRotLegL.IsTouching();
        hitRotLegR.IsTouching();
        hitRotSpine.IsTouching();
//		hitRotHips.IsTouching();
        hitRotUArmL.IsTouching(1, -1);
        hitRotUArmR.IsTouching(1, -1);
        hitRotLArmL.IsTouching();
        hitRotLArmR.IsTouching(-1);

        hitRotHips.Balance();
    }

    #endregion
}