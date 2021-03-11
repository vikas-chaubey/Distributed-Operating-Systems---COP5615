==================================================================PROJECT 1 ============================================================================================

Group Members: I have done this project alone, hence i dont have any group members.

Name  :  Vikas Chaubey
UFID  :  35115826
Email :  vikas.chaubey@ufl.edu


Note : Please use following command for program execution if following error occurs ( error FS3302: The package management feature requires language version 5.0 use /langversion:preview ))

      dotnet fsi --langversion:preview proj1.fsx  1000000 4



Problem : For the given input determine number of possible combinations of consecutive inetegrs such that sum of squares of these consecutive inetegers form a perfect square.

Given input : N (last number) ,K (length of the sequence) 

Implemented solution : 

In order to solve this problem efficiently i have implemented one master actor and 16 worker akka actors. After receiving input from command line, master actor spawns these 16 worker actors and divides the task evenly among these 16 worker actors.

Reason for having only 16 worker actors :

My personal laptop has I9 processor with 8 cores. so assuming that each actor will be handled by a separate thread and one thread will acquire 1 core.I made an assumption that i should start with 8 worker actors.
in order to find an efficient number of worker actors, I ran one same problem (N=10^6  k= 24) with different number of worker actors , initially i started with 8 noted the performance then increased the number to 16 , 32 , 64 and 128 sequentially. In this process i found that Performance(CPU/real time ratio) was significantly increased with 16 worker actors compared to only 8 worker actors.but on further increasing the number of worker actors to 32, 64 and 128, there was no significant change in the performance. Hence i decided to keep the number of worker actors as 16.



1) Size of the work unit :

	Given input is : N (last number) ,K (length of the sequence). The Master actor divides all the tasks evenly among 16 worker actors.

	if (N>=16) : then  work unit for each worker actor is  calulated by dviding the N (last number) by number of worker actors (16)

	  so work unit =  N(last number) / 16

	If N (last number) is not perfectly divisble by 16 then remainder is taken into consideration, and one more worker actor is spawned to process all remaining tasks.this case also applies if (N < 16).



2) The result of running the program for  :  dotnet fsi proj1.fsx 1000000 4

	Result : On running that input, Program gave no results, indicating that for this input there is no sequence of 4 consecutive integers , whose sum of squarres form a perfect squarre.


3) Running time and performance of the program :  dotnet fsi proj1.fsx 1000000 4

	Output of the program: 

	    Real: 00:00:00.000, CPU: 00:00:00.000, GC gen0: 0, gen1: 0, gen2: 0
		Real: 00:00:01.204, CPU: 00:00:10.162, GC gen0: 574, gen1: 2, gen2: 0

	    CPU / Real time ratio  = 10162/1204 =  8.44 ( ration > 1 , It shows that parallelization was acheieved ) 


4) The Biggest problem which i managed solved.

	Since My laptops has 16 Gb RAM and I9 processor with 8 cores i tried solving  N = 10^9  k = 24.

	The program was able to find all perfect square sum sequences , but it took approx. 57 minutes to solve that problem. With CPU / Real time ratio as : 9.31



5) Bonus :

 With akka i managed to do tasks with one remote and one local machine using akka remoting. On the local machine master actor lives.On the remote machine manager actor lives. when local machine master actor sends tasks to remote machine manager using remote messaging, manager actor on the remote machine spawns many actors to perform the tasks.After the task completion the results are sent back to master actor in the local node agin by using remote messaging.we have to use akka remoting and setup configuration in order to make remote actors to listen on specific ports and similar configurations have to be made for master node.











