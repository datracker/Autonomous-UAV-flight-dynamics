/* Flight dynamics of UAV given waypoints made in Unity3D game engine.
 * This code is a part of the UAV simulator.
 * Author: Kazi Tanvir Ahmed Siddiqui
 * Language: c#
 * Version: 1.0.0
 * Dated: April 09, 2017
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CSML;


public class FlightDynamics : MonoBehaviour {

	private Rigidbody rb;

	private double tsim, dt, deg2rad, rad2deg;

	//System param and Initial Condition 
	//private double m;
	private double g;
	private double k_th;
	private double l;
	private double k_m;
	private double Ixx, Iyy, Izz;
	private Matrix I;	//= new float [] { {Ixx, 0, 0}, {0,Iyy,0}, {0,0,Izz} };
	private double omega1, omega2, omega3, omega4;
	private double psi, theta, phi; //Euler Angles
	private Matrix pos, des_pos, posdot, des_posdot;
	private Matrix phi_theta_psi_dot, pqr, pqr_dot;
	private double pdot, qdot, rdot; //used instead of posdot 

	private double[ , ] waypoints;
	private double z_des, psi_des, r_des;

	//PD(proportional derivative) Control params
	private double kp_pitch, kd_pitch, kp_roll, kd_roll, kp_yaw, kd_yaw;
	private double kp_x, kd_x, kp_y, kd_y, kp_z, kd_z;


	private int i, k, time;
	private float error;


	//Controller Implementation and angular speed computation vars
	private double e_x, e_y, e_z, del_ax, del_ay, del_az;

	//Rotational Speed for hovering vars
	private double omega_h, phi_des, theta_des, del_omega_f, q_des, p_des;

	//Attitude control vars
	private double e_theta, e_phi, e_psi, del_omega_theta, del_omega_phi, del_omega_psi;
	private double total_omega;

	//Propeller Force, moments, and input params:
	private double f1, f2, f3, f4; // Individual thrust force
	private double m1, m2, m3, m4; //Moments by motors

	private double u1; //Thrust force input
	private double u2; //Roll control input
	private double u3; //Pitch control input
	private double u4; //Yaw control input

	//private float[,] r_psi; //=Rpsi, Yaw wrt Z axis (psi)
	//private float[,] r_theta; //=Rtheta, Pitch wrt Y axis (theta)
	//private float[,] r_phi; //=Rphi, Roll wrt X axis (phi)
	//private float[,] r_rt; //=R_rt
	private Matrix r_psi, r_theta, r_phi, r_rt, r_net, temp, temp_u, temp_dot, temp_crossProd; //r_rt = R-_t and r_net = R

	//private float[,] r_net;//=R,

	//Force equation vars:
	//private float[,] weight, forces, posddot; 
	private Matrix weight, forces, posddot;

	//Rate equation vars:
	private float [,] phi_theta_psi;
	//Result arrays:
	private float[] time_arr;
	private float[] xpos_arr, ypos_arr, zpos_arr;
	private float[] xpos_des_arr, ypos_des_arr, zpos_des_arr;
	private float[] phi_arr, theta_arr, psi_arr;
	private float[] omega1_arr, omega2_arr, omega3_arr, omega4_arr;
	private float[] p_arr, q_arr, r_arr;
	private float[] e_x_arr, e_y_arr, e_z_arr;
	private float[] del_ax_arr, del_ay_arr, del_az_arr;


	// Use this for initialization
	void Start () {
		//m = 1.08; //Should be rb.mass;
		dt = 0.1; //will be replaced by frame difference
		g = 9.81f;
		rb = GetComponent <Rigidbody> ();
		rb.mass = 1.08f; //is m (mass)
		k_th = 2.2f * Mathf.Pow (10, -4);
		l = 0.12f;
		k_m = 5.4f * Mathf.Pow (10, -6);
		Ixx = 2 * rb.mass * Mathf.Pow ((float)l, 2);
		Iyy = Ixx; //optimized
		Izz = 2 * Ixx; //optimized
		I = new Matrix (new double[,] { { Ixx, 0, 0 }, { 0, Iyy, 0 }, { 0, 0, Izz } });

		omega1 = 0;
		omega2 = 0;
		omega3 = 0;
		omega4 = 0;

		deg2rad = Mathf.PI / 180;
		rad2deg = 1 / deg2rad; //optimized

		psi = 0 * deg2rad;
		theta = 0 * deg2rad;
		phi = 0 * deg2rad;

		pos = new Matrix (new double[] { 0, 0, 0 });
		//Debug.Log(pos[1,1].Re);//works
		//Debug.Log(pos[2,1].Re);//works
		//Debug.Log(pos[3,1].Re);//works
		des_pos = new Matrix (new double[] {0, 0, 0}); // =pos_des; dicrepancy in the given code
		//Debug.Log(des_pos[3,1]); //works
		//Debug.Log(des_pos[2,1]); //works
		//Debug.Log(des_pos[1,1]); //works
		posdot = new Matrix (new double[] { 0, 0, 0 });
		//Debug.Log(posdot[3,1]); //works
		//Debug.Log(posdot[2,1]); //works
		//Debug.Log(posdot[1,1]); //works
		des_posdot = new Matrix (new double[] { 0, 0, 0 }); // =posdot_des
		//Debug.Log(des_posdot[3,1].Re); //works
		//Debug.Log(des_posdot[2,1].Re); //works
		//Debug.Log(des_posdot[1,1].Re); //works

		phi_theta_psi_dot = new Matrix (new double[] { 0, 0, 0 });
		pqr = new Matrix (new double[] { 0, 0, 0 });
		//pqr_dot = new Matrix (new double[] { 0, 0, 0 }); //=pqrdot

		//Confusion in Waypoints no height value was given
		//This should be user input
		waypoints = new double[6, 2] {
			{ 0, 0 }, 
			{ 10, 5 }, 
			{ 10, 20 },
			{ 15, 20 },
			{ 15, 30 },
			{ 20, 30 }
		};

		//Public variables, Need user input
		z_des = 25; //this is altitude
		psi_des = 0;
		r_des = 0;

		kp_pitch = 0.2f;
		kd_pitch = 10;
		kp_roll = 0.2f;
		kd_roll = 10;
		kp_yaw = 1;
		kd_yaw = 10;

		kp_x = 0.015f; 	
		kd_x = 0.2f;
		kp_y = 0.015f;
		kd_y = 0.2f;
		kp_z = 1;
		kd_z = 2;

		i = 1;
		k = 0;
		time = 0; // should update on every frame, might not need time in Unity
		error = 0.1f;
	}
	
	// Update is called once per frame
	void Update () {
		//*********Controller Implementation and Computation of desired Angular speed for motors*********
		//CSML matrix elements are complex type, we only need the real value, the imaginary part is always zero
		e_x = des_pos[1,1].Re - pos[1,1].Re; 
		e_y = des_pos[2,1].Re - pos[2,1].Re; 
		e_z = z_des - pos[3,1].Re;
		//Debug.Log(e_x); //ok-1
		//Debug.Log(e_y); //ok-2
		//Debug.Log(e_z); //ok-3

		del_ax = kp_x * e_x + kd_x * (des_posdot [1, 1].Re - posdot [1, 1].Re);
		del_ay = kp_y*e_y + kd_y*(des_posdot[2,1].Re-posdot[2,1].Re);
		del_az = kp_z*e_z + kd_z*(des_posdot[3,1].Re-posdot[3,1].Re);
		//Debug.Log (del_ax); //ok-1
		//Debug.Log (del_ay); //ok-1
		//Debug.Log (del_az); //ok-1


		//***********Rotational speed for hovering************
		//Debug.Log(rb.mass);
		omega_h = (rb.mass*9.8f) / (k_th*4);
		omega_h = Mathf.Pow ((float)omega_h, 0.5f);
		//Debug.Log (omega_h); //omega_h is good

		//Obtained by Linearization
		phi_des = (1 / g) * (del_ax * Mathf.Sin ((float)psi_des) - del_ay * Mathf.Cos ((float)psi_des));
		theta_des = (1 / g) * (del_ax * Mathf.Cos ((float)psi_des) + del_ay * Mathf.Sin ((float)psi_des));
		del_omega_f = (rb.mass * del_az) / (8 * k_th * omega_h);//rpob from del_az
		//Debug.Log(phi_des);	//ok-1
		//Debug.Log(theta_des); 	//ok-1
		//Debug.Log (del_omega_f); //ok-1

		q_des = (theta_des - theta) / dt; 
		p_des = (phi_des - phi) / dt;
		//Debug.Log (q_des);	//ok-1
		//Debug.Log (p_des);	//ok-1

		//**********Attitude Control*********
		//Debug.Log(theta_des); //prob
		//Debug.Log(phi_des); //prob
		//Debug.Log(psi_des); //okay

		e_theta = theta_des - theta; 
		e_phi = phi_des - phi;
		e_psi = psi_des - psi;

		//Debug.Log (e_theta);	//ok-1
		//Debug.Log (e_phi);	//ok-1
		//Debug.Log (e_psi);	//ok-1

		del_omega_theta = kp_pitch*e_theta + kd_pitch*(q_des - pqr[2,1].Re);
		del_omega_phi = kp_roll*e_phi + kd_roll*(p_des - pqr[1,1].Re);
		del_omega_psi = kp_yaw*e_psi + kd_yaw*(r_des - pqr[3,1].Re);
		//Debug.Log (del_omega_theta);//ok-1
		//Debug.Log (del_omega_phi);//ok-1
		//Debug.Log (del_omega_psi);//ok-1

		total_omega = omega_h + del_omega_f;
		//Debug.Log (total_omega); //ok-1

		//Debug.Log(del_omega_theta);
		//below all remains same to each other because del_omega_theta/phi/psi is always zero
		omega1 = total_omega - del_omega_theta + del_omega_psi; 
		omega2 = total_omega + del_omega_phi - del_omega_psi; 
		omega3 = total_omega + del_omega_theta + del_omega_psi;
		omega4 = total_omega - del_omega_phi - del_omega_psi;

		//Debug.Log (omega1);	//ok-1
		//Debug.Log (omega2);	//ok-1
		//Debug.Log (omega3);	//ok-1
		//Debug.Log (omega4);	//ok-1

		// *******Propeller Force, Moments, and Input parameters********
		//Individual thrust forces
		f1 = k_th * Mathf.Pow ((float)omega1, 2);
		f2 = k_th * Mathf.Pow ((float)omega2, 2);
		f3 = k_th * Mathf.Pow ((float)omega3, 2);
		f4 = k_th * Mathf.Pow ((float)omega4, 2);

		//Debug.Log (f1);	//ok-1
		//Debug.Log (f2);	//ok-1
		//Debug.Log (f3);	//ok-1
		//Debug.Log (f4);	//ok-1

		//Moments by motors
		m1 = k_m * Mathf.Pow ((float)omega1, 2);
		m2 = k_m * Mathf.Pow ((float)omega2, 2);
		m3 = k_m * Mathf.Pow ((float)omega3, 2);
		m4 = k_m * Mathf.Pow ((float)omega4, 2);

		//Debug.Log (m1);	//ok-1
		//Debug.Log (m2);	//ok-1
		//Debug.Log (m3);	//ok-1
		//Debug.Log (m4);	//ok-1

		u1 = f1 + f2 + f3 + f4;
		u2 = l * (f2 - f4);
		u3 = l * (f3 - f1);
		u4 = m1 - m2 + m3 - m4;

		//Debug.Log (u1);	//ok-1, alright
		//Debug.Log (u2);	//ok-2, prob
		//Debug.Log (u3);	//ok-3, prob
		//Debug.Log (u4);	//ok-4, prob

		r_psi = new Matrix (new double[,] {
			{ Mathf.Cos ((float)psi), Mathf.Sin ((float)psi), 0 }, 
			{ -Mathf.Sin ((float)psi), Mathf.Cos ((float)psi), 0 }, 
			{ 0, 0, 1 }
		});
		r_theta = new Matrix (new double[,] {
			{ Mathf.Cos ((float)theta), 0, -Mathf.Sin ((float)theta) },
			{ 0, 1, 0 },
			{ Mathf.Sin ((float)theta), 0, Mathf.Cos ((float)theta) }
		});
		r_phi = new Matrix (new double[,] {
			{ 1, 0, 0 },
			{ 0, Mathf.Cos ((float)phi), Mathf.Sin ((float)phi)},
			{ 0, -Mathf.Sin ((float)phi), Mathf.Cos ((float)phi)}
		});
		r_rt = new Matrix (new double[,] {
			{ 1, Mathf.Sin ((float)phi) * Mathf.Tan ((float)theta), Mathf.Cos ((float)phi) * Mathf.Tan ((float)theta) },
			{ 0, Mathf.Cos ((float)phi), -Mathf.Sin ((float)phi) },
			{ 0, Mathf.Sin ((float)phi) / Mathf.Cos ((float)theta), Mathf.Cos ((float)phi) / Mathf.Cos ((float)theta) }
		});

		//Debug.Log (r_psi);	//ok-1, prob
		//Debug.Log (r_theta);	//ok-1, prob
		//Debug.Log (r_phi);	//ok-1, prob
		//Debug.Log (r_rt); //ok-1

		r_net = r_phi * r_theta * r_psi;
		//Debug.Log (r_net); //ok-1, prob

		//*********Force Equation**************
		weight = new Matrix (new double[,] { {0}, {0}, {-rb.mass*g} });
		temp = new Matrix (new double[] {0, 0, u1});
		forces = r_net.Transpose () * temp; //r_net is wrong

		//Debug.Log (weight);	//ok-1, alright
		//Debug.Log (forces);	//ok-1, prob


		posddot = (forces + weight) / rb.mass;  //Ground Acceleration
		posdot += posddot * dt;		//Ground Speed
		pos += posdot * dt;		//Ground position 

		//Debug.Log (posddot);	//ok-1, prob
		//Debug.Log (posdot);	//ok-1
		//Debug.Log (pos);	//ok-1


		//*******Rate Equation********
		pdot = u2 / Ixx - pqr [2, 1].Re * pqr [3, 1].Re * (Izz - Iyy) / Ixx;
		qdot = u3 / Iyy - pqr [1, 1].Re * pqr [3, 1].Re * (Ixx - Izz) / Iyy;
		rdot = u4 / Izz - pqr [1, 1].Re * pqr [2, 1].Re * (Iyy - Ixx) / Izz;

		//Debug.Log (pdot);
		//Debug.Log (qdot);
		//Debug.Log (rdot);

		pqr_dot = new Matrix (new double[] { pdot, qdot, rdot }); //=pqrdotpqr_dot
		//Debug.Log (pqr_dot);	//ok-1

		pqr += pqr_dot*(float)dt;
		//Debug.Log (pqr);	//ok-1

		phi_theta_psi_dot = r_rt * pqr;
		//Debug.Log (phi_theta_psi_dot);	//ok-1

		phi += phi_theta_psi_dot [1, 1].Re * dt;
		theta += phi_theta_psi_dot [2, 1].Re * dt;
		psi += phi_theta_psi_dot [3, 1].Re * dt;

		Debug.Log (phi);	//ok-1
		Debug.Log (theta);	//ok-1
		Debug.Log (psi);	//ok-1

		/*Important: simulator's axes are not equivalent to real-drone axes.
		 *	x-axis(sim) = y-axis (real drone)
		 *	y-axis(sim) = z-axis(real drone)
		 *	z-axis(sim) = x-axis(real drone)
		 *	psi = angle w.r.t Z-axis
		 *	theta = angle w.r.t Y-axis
		 *	phi = angle w.r.t X-axis
		 */

		Vector3 posn = new Vector3 ((float)pos[2,1].Re,(float)pos[3,1].Re, (float)pos[1,1].Re); 
		rb.position = posn;
		rb.rotation = Quaternion.Euler ((float)phi, (float)theta, (float)psi);		

		//Debug.Log (pos);
		//Debug.Log (waypoints [k, 0]);
		//Debug.Log (error);

		if ( k < (waypoints.GetUpperBound(0))  &&
			 Mathf.Abs ((float)(pos [1, 1].Re - waypoints [k, 0])) < error &&
			 Mathf.Abs ((float)(pos [2, 1].Re - waypoints [k, 1])) < error )	
		{
			Debug.Log ("**************CHECKPOINT REACHED!!!*************");
			//Debug.Log ("Reached {0} th point");
			k += 1;
			des_pos [1, 1].Re = waypoints [k, 0];
			des_pos [1, 1].Im = 0;
			des_pos [2, 1].Re = waypoints [k, 1];
			des_pos [2, 1].Im = 0;
			//Debug.Log (waypoints.GetUpperBound (0)); //5 in the sample
			//Debug.Log (waypoints.GetLowerBound (0)); //0 in the sample
		}

		//Debug.Log (waypoints[waypoints.GetUpperBound (0), waypoints.GetLowerBound (0)]); 
		//Debug.Log (waypoints.GetLowerBound (0));
		if (Mathf.Abs ((float)(pos [1, 1].Re - waypoints [waypoints.GetUpperBound (0), waypoints.GetLowerBound (1)])) < error &&
			Mathf.Abs ((float)(pos [2, 1].Re - waypoints [waypoints.GetUpperBound (0), waypoints.GetUpperBound (1)])) < error) 
		{
			Debug.Log ("MISSION SUCCESSFUL");
		}	
	}
}