=================================DOS- PROJECT 4 - Twitter engine (Part 2) ========================================


Group Members: I have done this project alone, hence i dont have any group members.

Name  :  Vikas Chaubey
UFID  :  35115826
Email :  vikas.chaubey@ufl.edu


Note : Please use following command for program execution if following error occurs ( error FS3302: The package management feature requires language version 5.0 use /langversion:preview ))

     Run server first :  

	dotnet fsi --langversion:preview server.fsx 

     Once server is up and running then run client in separate terminal : 
		
	dotnet fsi --langversion:preview client.fsx (specify number of users) (specify session time , time duration between login and logout in seconds)

	ex. dotnet fsi --langversion:preview client.fsx 20 60


What is working?

With this project I have successfully implemented Twitter engine and its functionalities like user registration, login , session , logout, follow, tweet , retweet etc.

Clients and server are run in different processes and they communicate with each other using web sockets. These web sockets are implemented using Suave library

The performance of all the server endpoints were also tested during simulation and all the metrics have been recorded which is in project report.

The screenshots of simulation outputs are also aded in the folder.



The biggest problems solved:


Input :

Maximum number of  users simulated successfully : 900 , and maximum number of tweet requests simulated 3536 requests , for a session Tim of 300 second (5 minutes)
This is done on 8 core 16 gb machine.


This is the biggest problem I managed to solve, I also tried inputs such as 800, 1000 users but at that point aka actors stop communicating with each other due to increased load and scarce resources to process requests during simulation and hence the processes get stuck.


