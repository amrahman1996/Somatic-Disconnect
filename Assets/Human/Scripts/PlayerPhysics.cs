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
    public Dictionary<Collider, BodyPartClass> collToPart = new Dictionary<Collider, BodyPartClass>();
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


        private Vector3 _topVector;
        private readonly Dictionary<Collider, Vector3> _collToExtent = new Dictionary<Collider, Vector3>();
        public bool shouldHitRot, isTouching, isTouchingR;
        public Vector3 postForward;
        protected Quaternion preRot;
        protected Vector3 forward, up;
        private readonly Rigidbody _rb;
        private const float Mult = 25f;

        public void StoreForward(Vector3 forwardT) {
            forward = forwardT;
            if(bodyPart.name == "spine") {
//                Debug.DrawRay(bodyPart.transform.TransformPoint(_extents.right), bodyPart.transform.right, Color.red);
//                Debug.DrawRay(bodyPart.transform.TransformPoint(_extents.top), bodyPart.transform.up, Color.green);
//                Debug.DrawRay(bodyPart.transform.TransformPoint(_extents.front), bodyPart.transform.forward, Color.blue);
            }
        }

        public void FindBounds(IEnumerable<GameObject> colliderObjects) {
            Bounds bounds = new Bounds();
            foreach(GameObject co in colliderObjects) {
                Collider[] colliders = co.GetComponents<Collider>();
                foreach(Collider collider in colliders) {
                    bounds.Encapsulate(collider.bounds);
                    _collToExtent.Add(collider, collider.bounds.extents);
                }
            }
            _topVector = new Vector3(0, bodyPart.transform.InverseTransformPoint(bounds.max).y, 0);
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

        private Vector3 GetOctant(Collider collHit, Vector3 pointHit) {
            Vector3 fromBody = pointHit - bodyPart.transform.position;
            Vector3 fromColl = pointHit - collHit.bounds.center;
            Vector3 toTop = bodyPart.transform.TransformPoint(_topVector) - bodyPart.transform.position;
            Vector3 collExtents = _collToExtent.ContainsKey(collHit) ? _collToExtent[collHit] : collHit.bounds.extents;
            Vector3 toRight = bodyPart.transform.TransformDirection(new Vector3(collExtents.x, 0, 0));
            Vector3 toFront = bodyPart.transform.TransformDirection(new Vector3(0, 0, collExtents.z));
            if(bodyPart.name == "spine") {
                Debug.DrawRay(bodyPart.transform.position, toTop, Color.green);
                Debug.DrawRay(collHit.bounds.center, toRight, Color.red);
                Debug.DrawRay(collHit.bounds.center, toFront, Color.blue);
                Debug.DrawRay(bodyPart.transform.position, fromBody, Color.cyan);
                Debug.DrawRay(collHit.bounds.center, fromColl, Color.magenta);
            }
            float upDown = Vector3.Dot(toTop, fromBody) / Vector3.SqrMagnitude(toTop);
            float leftRight = Vector3.Dot(toRight, fromColl) / Vector3.SqrMagnitude(toRight); //TODO: Need toRight etc to be projections cause of this I think
            float frontBack = Vector3.Dot(toFront, fromColl) / Vector3.SqrMagnitude(toFront);
            Vector3 info = new Vector3(upDown, leftRight, frontBack);
            if(bodyPart.name == "spine") {
                Debug.Log("upDown: " + upDown + " : " + "frontBack: " + frontBack + " : " + "leftRight: " + leftRight);
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
        public virtual void HitCalc(Vector3 point, Vector3 direction, Vector3 rVelocity, float mass, Collider collHit) {
            shouldHitRot = true; //enable HitRot() to apply rotation
            isTouching = true; //enable IsTouching() for continuous touching
            collisionNormal = direction;
            positionVector = point - bodyPart.transform.position; //TODO why is this normalized?
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
            GetOctant(collHit, point);
            AddTorque(point, rVelocity, mass, torquePlus);
//				Debug.Log(hitRotDot + " : " + torquePlus.sqrMagnitude);
        }

        private void AddTorque(Vector3 point, Vector3 rVelocity, float mass, Vector3 torquePlus) {
            if(!(Vector3.Dot(torquePlus, rotVector) < torquePlus.sqrMagnitude * 0.8f))
                return; //if torquePlus is too close to what rotVector already is

            torqueVector += torquePlus; //FIXME should include Time.fixedDeltaTime

            //force that is parallel to limb or too close to hinge, so can't rotate it, but can be transferred to parent. 	
            tForceVector = -0.2f * (rVelocity - forceVector) - collisionNormal * (forceVector.magnitude - Vector3.Cross(positionVector, forceVector).magnitude);
            if(parentPart != null) {
                parentPart.HitTransfer(bodyPart.transform.position, rVelocity, collisionNormal, tForceVector, mass);
            }
        }

        protected virtual void HitTransfer(Vector3 point, Vector3 rVelocity, Vector3 direction, Vector3 transferredForceVector, float mass) {
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

        public void AddChildren(BodyPartClass armL, BodyPartClass armR) {
            _armL = armL;
            _armR = armR;
        }

        #endregion

        public float rotTest;

        #endregion
    }

    [Serializable]
    public class LegClass : BodyPartClass {
        #region Variables

        public readonly GameObject thigh, shin, foot;
        public float stepFrontAmount, stepSideAmount;
        private const float Mult = 12.5f;
        private Vector3 _rV;
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
        collToPart.Add(hipsC.GetComponent<Collider>(), hitRotHips);
        collToPart.Add(shinLC.GetComponent<Collider>(), hitRotLegL);
        collToPart.Add(footLObjC.GetComponent<Collider>(), hitRotLegL);
        collToPart.Add(thighLC.GetComponent<Collider>(), hitRotLegL);
        collToPart.Add(shinRC.GetComponent<Collider>(), hitRotLegR);
        collToPart.Add(footRObjC.GetComponent<Collider>(), hitRotLegR);
        collToPart.Add(thighRC.GetComponent<Collider>(), hitRotLegR);
        collToPart.Add(spineC.GetComponent<Collider>(), hitRotSpine);
        collToPart.Add(ribsC.GetComponent<Collider>(), hitRotSpine);
        collToPart.Add(headC.GetComponent<Collider>(), hitRotSpine);
        collToPart.Add(lowerArmLObjC.GetComponent<Collider>(), hitRotLArmL);
        collToPart.Add(handLObj.GetComponent<Collider>(), hitRotLArmL);
        collToPart.Add(lowerArmRObjC.GetComponent<Collider>(), hitRotLArmR);
        collToPart.Add(handRObj.GetComponent<Collider>(), hitRotLArmR);
        collToPart.Add(upperArmLObjC.GetComponent<Collider>(), hitRotUArmL);
        collToPart.Add(upperArmRObjC.GetComponent<Collider>(), hitRotUArmR);
        hitRotHips.AddChildren(hitRotSpine, hitRotLegL, hitRotLegR);
        hitRotLegL.AddParent(hitRotHips);
        hitRotLegR.AddParent(hitRotHips);
        hitRotSpine.AddParent(hitRotHips);
        hitRotSpine.AddChildren(hitRotUArmL, hitRotUArmR);
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

    private void OnCollisionEnter(Collision collInfo) {
        if(collInfo.gameObject.GetComponent<Rigidbody>()) {
            foreach(ContactPoint c in collInfo.contacts) {
                BodyPartClass part = collToPart.ContainsKey(c.thisCollider) ? collToPart[c.thisCollider] : hitRotLArmR;
                part.isTouchingR = true;
                part.HitCalc(c.point, c.normal, collInfo.relativeVelocity, collInfo.gameObject.GetComponent<Rigidbody>().mass, c.thisCollider);
            }
        } else {
            foreach(ContactPoint c in collInfo.contacts) {
                BodyPartClass part = collToPart.ContainsKey(c.thisCollider) ? collToPart[c.thisCollider] : hitRotLArmR;
                part.HitCalc(c.point, c.normal, collInfo.relativeVelocity, 1000, c.thisCollider);
            }
        }
    }

    private void OnCollisionStay(Collision collInfo) {
        if(collInfo.gameObject.GetComponent<Rigidbody>()) {
            foreach(ContactPoint c in collInfo.contacts) {
                BodyPartClass part = collToPart.ContainsKey(c.thisCollider) ? collToPart[c.thisCollider] : hitRotLArmR;
                part.isTouchingR = true;
                if(part != hitRotLegR && part != hitRotLegR) //TODO: TEMP remove this
                    part.HitCalc(c.point, c.normal, collInfo.relativeVelocity, collInfo.gameObject.GetComponent<Rigidbody>().mass, c.thisCollider);
            }
        } else {
            foreach(ContactPoint c in collInfo.contacts) {
                BodyPartClass part = collToPart.ContainsKey(c.thisCollider) ? collToPart[c.thisCollider] : hitRotLArmR;
                if(part != hitRotLegR && part != hitRotLegR) //TODO: TEMP remove this
                    part.HitCalc(c.point, c.normal, collInfo.relativeVelocity, 1000, c.thisCollider);
            }
        }
    }

    public void OnBullet(object[] hitInfo) {
        RaycastHit hit = (RaycastHit) hitInfo[0];
        BodyPartClass part = collToPart.ContainsKey(hit.collider) ? collToPart[hit.collider] : hitRotLArmR;
        part.HitCalc(hit.point, -hit.normal, (Vector3) hitInfo[1], 1.2f, hit.collider);
    }

    private void FixedUpdate() {
        foreach(var part in collToPart.Values) part.TouchReset();
    }

    public float crouchAmountP, crouchAmountSmooth;

    private void Update() {
        crouchAmountSmooth = Extensions.SharpInDamp(crouchAmountSmooth, crouchAmountP, 3.0f);
        crouchAmountP -= crouchAmountP * Time.deltaTime * 2;
        crouchAmount = crouchAmountSmooth;
        foreach(var part in collToPart.Values) part.StoreForward(part.bodyPart.transform.forward);


        hitRotLArmR.GetParentAngles();
    }

    private void LateUpdate() {
//		hitRotLegL.LegControl(xxx, xxx, xxx1);
//        hitRotLegR.HitRot();
//		hitRotLegR.LegControl(xxx, xxx, xxx1);
        foreach(var part in collToPart.Values) {
            if(part != hitRotLegL && part != hitRotLegR) //TODO: TEMP remove this
                part.HitRot();
            part.postForward = part.bodyPart.transform.forward;
            part.IsTouching();
        }
//        hitRotUArmL.IsTouching(1, -1); //TODO: Configure these within the class itself
//        hitRotUArmR.IsTouching(1, -1);
//        hitRotLArmR.IsTouching(-1);

        hitRotHips.Balance();
    }

    #endregion
}