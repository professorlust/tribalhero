<?php
                // Generated by DatabaseGenerator program on 9/21/2010 11:49:18 PM
	            $techKey = 'TUNNEL_EXCAVATION_TECHNOLOGY';
	            $techName = 'Tunnel Excavation Research';
	
	            $trainedBy = array('name' => 'Foundry', 'key' => 'FOUNDRY_STRUCTURE', 'level' => '3');
	
	            // Levels array should contain:
	            $levels = array(
		            array('description' => 'Improves underground engineering.  Reduces distance requirement by 1 for underground structures.', 'time' => '90000', 'gold' => 0, 'crop' => 500, 'iron' => 0, 'labor' => 0, 'wood' => 500, 'requirements' => array()),
array('description' => 'Improves underground engineering.  Reduces distance requirement by 2 for underground structures.', 'time' => '108000', 'gold' => 250, 'crop' => 0, 'iron' => 0, 'labor' => 0, 'wood' => 1000, 'requirements' => array()),
array('description' => 'Improves underground engineering.  Reduces distance requirement by 3 for underground structures.', 'time' => '126000', 'gold' => 500, 'crop' => 0, 'iron' => 0, 'labor' => 0, 'wood' => 2000, 'requirements' => array()),

	            );

                include '/../technology_view.ctp';
            