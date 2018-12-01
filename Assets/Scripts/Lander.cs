using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Lander : MonoBehaviour {
	enum Status { settingup, ready, playing, survived, crashed, lost }

    public GameObject yellowPiecePrefab;
    public GameObject blackPiecePrefab;

	public DdamaBoard ddamaBoard;
	
	private GameObject go;
	private Rigidbody rb;
	private Status status;

	// Use this for initialization
	void Start () {
		Physics.gravity = new Vector3(0.0f, -3.0f, 0.0f);
	}
	
	public void PlayRound(Piece.Team team) {
		status = Status.settingup;
		GameObject piecePrefab = (team == Piece.Team.Yellow)
			? yellowPiecePrefab : blackPiecePrefab;

		rb = null;

		go = Instantiate(piecePrefab) as GameObject;
		go.transform.SetParent(transform);
		go.transform.position = transform.position + (Vector3.up * 6.0f);
		go.transform.rotation = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);
		status = Status.ready;
	}

	void StartPlaying() {
		status = Status.playing;
		rb = go.AddComponent<Rigidbody>();
		rb.mass = 1;
	}

	void ThrusterOn() {
		rb.AddForce(Vector3.up * 200.0f);
	}

	void ThrusterOff() {
		rb.AddForce(Vector3.zero);
	}

	void CheckPlayerLost() {
		if ((transform.position - go.transform.position).magnitude > 20.0f) {
			status = Status.lost;
			EndRound();
		}
	}

	// Update is called once per frame
	void Update () {
		if (status == Status.ready && Input.GetMouseButtonDown(0)) {
			StartPlaying();
		}

		if (status == Status.playing) {
			if (Input.GetMouseButtonDown(0))
				ThrusterOn();

			if (Input.GetMouseButtonUp(0))
				ThrusterOff();

			CheckPlayerLost();
		}

		if ((status == Status.crashed || status == Status.survived) &&  Input.GetMouseButtonDown(0)) {
			EndRound();
		}
	}

	void OnCollisionEnter(Collision col) {
		if (status != Status.playing) return;
		status = col.relativeVelocity.magnitude < 2.0 ? Status.survived : Status.crashed;
	}

	void EndRound() {
		Destroy(go);
		bool survived = status == Status.survived;
		status = Status.settingup;
		ddamaBoard.EndMiniGame(survived);
	}
}
